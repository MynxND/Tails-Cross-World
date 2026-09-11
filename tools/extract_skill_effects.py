"""Statically translate Twelve Tails legacy skill metadata and combat constants.

The legacy C# files are evidence only. This module reads them as text and never
imports, compiles, or executes them.
"""

from __future__ import annotations

import argparse
import ast
from dataclasses import dataclass
import json
from pathlib import Path
import re
from typing import Any, Iterable


SPECIES = (
    "Wolf", "Bison", "Panda", "Whale", "Cat", "Chameleon",
    "Mole", "Rabbit", "Monkey", "Sheep", "Penguin", "Bat",
)

DEFAULT_COMBAT_FIELDS: dict[str, Any] = {
    "name_key": "",
    "animation_clip": "",
    "damage": 0,
    "cooldown_seconds": 0.0,
    "hit_delay_seconds": 0.0,
    "hit_count": 1,
    "hit_interval_seconds": 0.0,
    "hit_times_seconds": [],
    "action_duration_seconds": 0.0,
    "combo_window_start_seconds": 0.0,
    "combo_window_end_seconds": 0.0,
    "combo_next_skill_id": "",
    "resource_cost": 0,
    "range": 0.0,
    "target_shape": "sphere",
    "target_width": 0.0,
    "target_height": 0.0,
    "max_targets": 1,
    "projectile_speed": 0.0,
    "projectile_lifetime_seconds": 0.0,
    "projectile_homing_radians_per_second": 0.0,
    "impact_radius": 0.0,
    "status_effect_id": "",
    "status_duration_seconds": 0.0,
    "status_tick_seconds": 0.0,
    "status_damage_per_tick": 0,
    "status_movement_multiplier": 1.0,
}


class ExtractionError(RuntimeError):
    pass


@dataclass
class SkillState:
    cost_mp: int = 0
    cost_sp: int = 0
    cost_mana: int = 0
    req_level: int = -1
    req_points: int = -1
    raw_req_skill: int = 0
    skill_type: str = "normal"
    mode: str = "passive"
    target: str = "self"
    effect_key: str = ""


@dataclass(frozen=True)
class Signal:
    kind: str
    target: str = ""


def _method_body(source: str, signature: str) -> str:
    match = re.search(signature, source)
    if match is None:
        raise ExtractionError(f"method not found: {signature}")
    opening = source.find("{", match.end())
    if opening < 0:
        raise ExtractionError(f"method body not found: {signature}")
    depth = 0
    in_string = False
    escaped = False
    for index in range(opening, len(source)):
        char = source[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[opening + 1:index]
    raise ExtractionError(f"unclosed method body: {signature}")


def _skill_inventory(source: str) -> tuple[list[str], dict[int, str]]:
    body = _method_body(source, r"public\s+static\s+string\s+getSkillTree\s*\(")
    command_map: dict[int, str] = {}
    current_commands: list[int] = []
    for line in body.splitlines():
        case = re.match(r"\s*case\s+(\d+)\s*:", line)
        if case:
            current_commands.append(int(case.group(1)))
            continue
        condition = re.search(r"(?:EqualityOperator\(\s*commandNum\s*,|commandNum\s*==)\s*(\d+)", line)
        if condition:
            current_commands = [int(condition.group(1))]
            continue
        result = re.search(r'result\s*=\s*"([^"]+)"\s*;', line)
        if result and result.group(1) != "none":
            for command in current_commands:
                command_map[command] = result.group(1)
            current_commands.clear()
        elif "break;" in line:
            current_commands.clear()
    ids = list(command_map.values())
    if len(ids) != len(set(ids)):
        raise ExtractionError("getSkillTree contains duplicate skill IDs")
    return ids, command_map


def _apply_metadata(line: str, state: SkillState) -> Signal | None:
    match = re.search(r"skillClass\.setReq\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", line)
    if match:
        if state.req_level == -1:
            state.req_level = int(match.group(1))
        if state.req_points == -1:
            state.req_points = int(match.group(2))
    match = re.search(r"skillClass\.setMP\(\s*(-?\d+)\s*\)", line)
    if match and state.cost_mp == 0:
        state.cost_mp = int(match.group(1))
    match = re.search(r"skillClass\.setSP\(\s*(-?\d+)\s*\)", line)
    if match and state.cost_sp == 0:
        state.cost_sp = int(match.group(1))
    match = re.search(r"skillClass\.setMPSP\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", line)
    if match:
        if state.cost_mp == 0:
            state.cost_mp = int(match.group(1))
        if state.cost_sp == 0:
            state.cost_sp = int(match.group(2))
    for field, attribute in (("type", "skill_type"), ("mode", "mode"), ("target", "target")):
        match = re.search(rf"skillClass\.{field}\s*=\s*eSkill\w+\.([A-Za-z]+)\s*;", line)
        if match:
            setattr(state, attribute, match.group(1))
    match = re.search(r'skillClass\.cType\s*=\s*"([^"]*)"\s*;', line)
    if match:
        state.effect_key = match.group(1)
    match = re.search(r"skillClass\.cMana\s*=\s*(-?\d+)\s*;", line)
    if match:
        state.cost_mana = int(match.group(1))
    match = re.search(r"skillClass\.rSkill\s*=\s*(-?\d+)\s*;", line)
    if match:
        state.raw_req_skill = int(match.group(1))
    match = re.search(r'goto\s+case\s+"([^"]+)"\s*;', line)
    if match:
        return Signal("goto_case", match.group(1))
    match = re.search(r"goto\s+(IL_[0-9A-Fa-f]+)\s*;", line)
    if match:
        return Signal("goto_label", match.group(1))
    if "return skillClass;" in line:
        return Signal("stop")
    if re.match(r"\s*break\s*;", line):
        return Signal("break_switch")
    return None


def _switch_skill_state(body: str, skill_id: str) -> SkillState:
    lines = body.splitlines()
    cases: dict[str, tuple[int, int]] = {}
    starts: list[tuple[str, int]] = []
    switch_start = next((index for index, line in enumerate(lines) if re.search(r"\bswitch\s*\(", line)), None)
    if switch_start is None:
        raise ExtractionError("getSkill switch not found")
    switch_opening = switch_start
    while switch_opening < len(lines) and "{" not in lines[switch_opening]:
        switch_opening += 1
    depth = 0
    switch_end = -1
    for index in range(switch_opening, len(lines)):
        depth += lines[index].count("{") - lines[index].count("}")
        if depth == 0:
            switch_end = index
            break
    if switch_end < 0:
        raise ExtractionError("unclosed getSkill switch")
    for index, line in enumerate(lines):
        match = re.match(r'\s*case\s+"([^"]+)"\s*:', line)
        if match:
            starts.append((match.group(1), index + 1))
    for position, (case_id, start) in enumerate(starts):
        end = starts[position + 1][1] - 1 if position + 1 < len(starts) else switch_end
        cases[case_id] = (start, end)
    if skill_id not in cases:
        raise ExtractionError(f"getSkill case missing for {skill_id}")
    state = SkillState()
    current = skill_id
    visited: set[str] = set()
    while current:
        if current in visited:
            raise ExtractionError(f"cyclic goto case for {skill_id}")
        visited.add(current)
        if current not in cases:
            raise ExtractionError(f"goto case target missing: {current}")
        start, end = cases[current]
        next_case = ""
        for line in lines[start:end]:
            signal = _apply_metadata(line, state)
            if signal is None:
                continue
            if signal.kind == "goto_case":
                next_case = signal.target
                break
            if signal.kind == "break_switch":
                for suffix_line in lines[switch_end + 1:]:
                    suffix_signal = _apply_metadata(suffix_line, state)
                    if suffix_signal is not None and suffix_signal.kind == "stop":
                        return state
                return state
            if signal.kind == "stop":
                return state
        current = next_case
    return state


def _brace_block(lines: list[str], header: int, limit: int) -> tuple[int, int]:
    opening = header
    while opening < limit and "{" not in lines[opening]:
        opening += 1
    if opening >= limit:
        raise ExtractionError(f"missing block after line: {lines[header].strip()}")
    depth = 0
    for index in range(opening, limit):
        depth += lines[index].count("{") - lines[index].count("}")
        if depth == 0:
            return opening + 1, index
    raise ExtractionError(f"unclosed block after line: {lines[header].strip()}")


def _skill_condition(line: str, skill_id: str) -> bool:
    match = re.search(r'skillname\s*==\s*"([^"]+)"', line)
    if match is None:
        raise ExtractionError(f"unsupported getSkill condition: {line.strip()}")
    result = skill_id == match.group(1)
    return not result if re.search(r"!\s*\(", line) else result


def _execute_nested(lines: list[str], start: int, end: int, skill_id: str, state: SkillState) -> Signal | None:
    index = start
    while index < end:
        stripped = lines[index].strip()
        if stripped.startswith("if ") or stripped.startswith("if("):
            branches: list[tuple[bool | None, int, int]] = []
            cursor = index
            after_chain = index + 1
            while cursor < end:
                header = lines[cursor].strip()
                if header.startswith("if") or header.startswith("else if"):
                    condition: bool | None = _skill_condition(header, skill_id)
                elif header.startswith("else"):
                    condition = None
                else:
                    break
                block_start, block_end = _brace_block(lines, cursor, end)
                branches.append((condition, block_start, block_end))
                after_chain = block_end + 1
                cursor = after_chain
                while cursor < end and not lines[cursor].strip():
                    cursor += 1
                if cursor >= end or not lines[cursor].strip().startswith("else"):
                    break
            for condition, block_start, block_end in branches:
                if condition is None or condition:
                    signal = _execute_nested(lines, block_start, block_end, skill_id, state)
                    if signal is not None:
                        return signal
                    break
            index = after_chain
            continue
        signal = _apply_metadata(lines[index], state)
        if signal is not None:
            return signal
        index += 1
    return None


def _nested_skill_state(body: str, skill_id: str) -> SkillState:
    lines = body.splitlines()
    labels: dict[str, tuple[int, int]] = {}
    label_starts: list[tuple[str, int]] = []
    for index, line in enumerate(lines):
        match = re.match(r"\s*(IL_[0-9A-Fa-f]+)\s*:", line)
        if match:
            label_starts.append((match.group(1), index + 1))
    prefix_end = label_starts[0][1] - 1 if label_starts else len(lines)
    for position, (label, start) in enumerate(label_starts):
        end = label_starts[position + 1][1] - 1 if position + 1 < len(label_starts) else len(lines)
        labels[label] = (start, end)
    state = SkillState()
    signal = _execute_nested(lines, 0, prefix_end, skill_id, state)
    visited: set[str] = set()
    while signal is not None and signal.kind == "goto_label":
        if signal.target in visited or signal.target not in labels:
            raise ExtractionError(f"invalid label path for {skill_id}: {signal.target}")
        visited.add(signal.target)
        signal = _execute_nested(lines, *labels[signal.target], skill_id, state)
    return state


def _tree_entry(skill_id: str, character_id: str, state: SkillState, ids: set[str], command_map: dict[int, str]) -> dict[str, Any]:
    tier_match = re.search(r"(\d+)$", skill_id)
    tier = int(tier_match.group(1)) if tier_match else 0
    line = skill_id[:tier_match.start()] if tier_match else skill_id
    if state.raw_req_skill:
        req_skill: str | int = command_map.get(state.raw_req_skill, state.raw_req_skill)
    elif tier > 1 and f"{line}{tier - 1}" in ids:
        req_skill = f"{line}{tier - 1}"
    else:
        req_skill = ""
    return {
        "id": skill_id,
        "character_id": character_id,
        "line": line,
        "tier": tier,
        "type": state.skill_type,
        "mode": state.mode,
        "target": state.target,
        "effect_key": state.effect_key,
        "cost_mp": state.cost_mp,
        "cost_sp": state.cost_sp,
        "cost_mana": state.cost_mana,
        "req_level": state.req_level,
        "req_points": state.req_points,
        "req_skill": req_skill,
    }


def extract_skill_tree(source_root: Path) -> list[dict[str, Any]]:
    entries: list[dict[str, Any]] = []
    for species in SPECIES:
        source = (source_root / f"{species}Skill.cs").read_text(encoding="utf-8-sig")
        ids, command_map = _skill_inventory(source)
        body = _method_body(source, r"public\s+static\s+SkillClass\s+getSkill\s*\(")
        for skill_id in ids:
            state = _nested_skill_state(body, skill_id) if species == "Whale" else _switch_skill_state(body, skill_id)
            entries.append(_tree_entry(skill_id, species.lower(), state, set(ids), command_map))
    return entries


def _split_arguments(arguments: str) -> list[str]:
    result: list[str] = []
    start = 0
    depth = 0
    in_string = False
    escaped = False
    for index, char in enumerate(arguments):
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
        elif char == '"':
            in_string = True
        elif char in "([":
            depth += 1
        elif char in ")]":
            depth -= 1
        elif char == "," and depth == 0:
            result.append(arguments[start:index].strip())
            start = index + 1
    result.append(arguments[start:].strip())
    return result


def _calls(source: str, name: str) -> list[tuple[int, list[str]]]:
    calls: list[tuple[int, list[str]]] = []
    for match in re.finditer(re.escape(name) + r"\s*\(", source):
        start = match.end()
        depth = 1
        in_string = False
        escaped = False
        for index in range(start, len(source)):
            char = source[index]
            if in_string:
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == '"':
                    in_string = False
            elif char == '"':
                in_string = True
            elif char == "(":
                depth += 1
            elif char == ")":
                depth -= 1
                if depth == 0:
                    calls.append((match.start(), _split_arguments(source[start:index])))
                    break
    return calls


def _numeric(expression: str, tier: int) -> float | None:
    value = expression.strip()
    wrapper = re.fullmatch(r"(?:[A-Za-z_][A-Za-z0-9_.]*\.)?(?:agiAdjust|talAdjust)\s*\((.*)\)", value, re.DOTALL)
    if wrapper:
        value = wrapper.group(1)
    range_scaled = re.fullmatch(
        r"(?:(.+?)\s*\*\s*[A-Za-z_][A-Za-z0-9_.]*\.rangeMod|[A-Za-z_][A-Za-z0-9_.]*\.rangeMod\s*\*\s*(.+))",
        value,
        re.DOTALL,
    )
    if range_scaled:
        value = next(group for group in range_scaled.groups() if group is not None)
    value = re.sub(r"\((?:int|float|double)\)", "", value)
    value = re.sub(r"(?i)\b[A-Za-z_][A-Za-z0-9_]*sLv[A-Za-z0-9_]*\b", str(tier), value)
    value = re.sub(r"(?<=\d)f\b", "", value)
    if re.search(r"[^0-9+\-*/().\s]", value):
        return None
    try:
        node = ast.parse(value, mode="eval")
    except SyntaxError:
        return None
    allowed = (ast.Expression, ast.Constant, ast.UnaryOp, ast.UAdd, ast.USub, ast.BinOp, ast.Add, ast.Sub, ast.Mult, ast.Div)
    if any(not isinstance(item, allowed) for item in ast.walk(node)):
        return None
    try:
        result = eval(compile(node, "<legacy-number>", "eval"), {"__builtins__": {}}, {})
    except (ArithmeticError, TypeError, ValueError):
        return None
    return float(result) if isinstance(result, (int, float)) and not isinstance(result, bool) else None


def _resolved_numeric(expression: str, tier: int, region: str, seen: frozenset[str] = frozenset()) -> float | None:
    value = _numeric(expression, tier)
    if value is not None:
        return value
    identifier = expression.strip()
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", identifier) or identifier in seen:
        return None
    assignments = re.findall(rf"(?m)\b{re.escape(identifier)}\s*=\s*([^;]+);", region)
    if not assignments:
        return None
    resolved = {
        item
        for assignment in assignments
        if (item := _resolved_numeric(assignment, tier, region, seen | {identifier})) is not None
    }
    return next(iter(resolved)) if len(resolved) == 1 and len(resolved) == len(assignments) else None


def _matching_block(source: str, opening: int) -> tuple[int, int]:
    depth = 0
    in_string = False
    escaped = False
    for index in range(opening, len(source)):
        char = source[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
        elif char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return opening + 1, index
    raise ExtractionError("unclosed combat block")


def _effect_regions(source: str, effect_key: str) -> list[str]:
    spans: list[tuple[int, int]] = []
    escaped = re.escape(effect_key)
    declarations = re.compile(rf"(?im)^\s*(?:internal\s+sealed\s+class|public\s+(?:virtual\s+)?[\w<>]+)\s+[^\r\n{{]*(?:RPC_)?{escaped}[^\r\n{{]*\s*{{")
    for match in declarations.finditer(source):
        opening = source.find("{", match.start(), match.end())
        start, end = _matching_block(source, opening)
        spans.append((start, end))
    marker_pattern = rf'myCommand\s*=\s*"{escaped}"|addTimeOut\(\s*"{escaped}"|PlayAnimation\(\s*"{escaped}"'
    for marker in re.finditer(marker_pattern, source, re.IGNORECASE):
        declarations_before = list(re.finditer(r"(?im)^\s*internal\s+sealed\s+class\s+[^\r\n{]+\s*{", source[:marker.start()]))
        if declarations_before:
            declaration = declarations_before[-1]
            opening = source.find("{", declaration.start(), declaration.end())
            start, end = _matching_block(source, opening)
            spans.append((start, end))
    unique_spans = list(dict.fromkeys(spans))
    leaf_spans = [
        span
        for span in unique_spans
        if not any(span[0] < other[0] and other[1] < span[1] for other in unique_spans)
    ]
    return [source[start:end] for start, end in leaf_spans]


def _alias_animations(source: str, effect_key: str) -> list[str]:
    escaped = re.escape(effect_key)
    excluded = {"addTimeOut", "removeTimeOut", "PlayAnimation", "ActionEvent"}
    aliases = {
        alias
        for alias in re.findall(rf'\b([A-Za-z_][A-Za-z0-9_]*)\s*\(\s*"{escaped}"\s*,', source, re.IGNORECASE)
        if alias not in excluded
    }
    animations: list[str] = []
    for alias in aliases:
        declarations = re.compile(rf"(?im)^\s*(?:internal\s+sealed\s+class|public\s+(?:virtual\s+)?[\w<>]+)\s+[^\r\n{{]*{re.escape(alias)}[^\r\n{{]*\s*{{")
        for match in declarations.finditer(source):
            opening = source.find("{", match.start(), match.end())
            start, end = _matching_block(source, opening)
            animations.extend(re.findall(r'PlayAnimation\(\s*"([^"]+)"', source[start:end]))
    return list(dict.fromkeys(animations))


def _single(values: Iterable[Any]) -> Any | None:
    unique = list(dict.fromkeys(value for value in values if value is not None))
    return unique[0] if len(unique) == 1 else None


def _has_projectile_delivery(region: str) -> bool:
    return any(
        re.search(pattern, region)
        for pattern in (
            r"\bPhotonNetwork\.Instantiate\s*\(",
            r"\.rigidbody\.velocity\s*=",
            r"\.velocity\s*=\s*[^;]+",
        )
    )


def _runtime_skill_id(character_id: str, effect_key: str, tier: int) -> str:
    normalized_effect = re.sub(r"[^a-z0-9]+", "_", effect_key.lower()).strip("_")
    return f"skill.legacy.{character_id}.{normalized_effect}.{tier}"


def _combat_entry(character_id: str, tree_entry: dict[str, Any], source: str) -> tuple[dict[str, Any] | None, list[str]]:
    effect_key = tree_entry["effect_key"]
    regions = _effect_regions(source, effect_key)
    if not regions:
        return None, ["no command/RPC region"]
    combined = "\n".join(regions)
    tier = tree_entry["tier"]
    reasons: list[str] = []
    animations = re.findall(r'PlayAnimation\(\s*"([^"]+)"', combined)
    if not animations:
        animations = _alias_animations(source, effect_key)
    animation = animations[0] if animations else None
    if animation is None:
        reasons.append("animation missing")

    timeout_values: list[float | None] = []
    for _, arguments in _calls(combined, "addTimeOut"):
        if len(arguments) >= 2 and arguments[0].strip(' "') == effect_key:
            timeout_values.append(_resolved_numeric(arguments[1], tier, combined))
    cooldown = _single(timeout_values) if timeout_values and all(value is not None for value in timeout_values) else 0.0 if not timeout_values else None
    if cooldown is None:
        reasons.append("cooldown expression missing or dynamic")

    damages: list[float | None] = []
    damage_positions: list[int] = []
    hit_calls = _calls(combined, ".hit")
    damage_calls = ((position, arguments, 2) for position, arguments in hit_calls)
    if not hit_calls:
        damage_calls = ((position, arguments, 1) for position, arguments in _calls(combined, "RPC_AddEffectDamage"))
    for position, arguments, damage_index in damage_calls:
        damage_positions.append(position)
        if len(arguments) <= damage_index:
            continue
        damages.append(_resolved_numeric(arguments[damage_index], tier, combined))
    damage = _single(damages) if damages and all(value is not None for value in damages) else None
    if damage is None or damage < 0 or not float(damage).is_integer():
        reasons.append("damage expression missing, dynamic, or ambiguous")

    target_shape = "oriented_box"
    rectangle_calls = _calls(combined, "Damage.FindRecTarget")
    rectangles: list[tuple[float, float, float] | None] = []
    for _, arguments in rectangle_calls:
        if len(arguments) >= 5:
            dimensions = tuple(_resolved_numeric(arguments[index], tier, combined) for index in (2, 3, 4))
            if all(value is not None and value > 0 for value in dimensions):
                rectangles.append(dimensions)  # type: ignore[arg-type]
            else:
                rectangles.append(None)
    target_dimensions = _single(rectangles) if rectangles and all(value is not None for value in rectangles) else None
    if target_dimensions is None:
        area_ranges: list[float | None] = []
        for _, arguments in _calls(combined, "Damage.FindAreaTarget"):
            area_ranges.append(_resolved_numeric(arguments[1], tier, combined) if len(arguments) >= 2 else None)
        area_range = _single(area_ranges) if area_ranges and all(value is not None for value in area_ranges) else None
        if area_range is not None and area_range > 0:
            target_shape = "sphere"
            target_dimensions = (0.0, 0.0, area_range)
        else:
            radial_ranges: list[float | None] = []
            for call_name in ("Damage.FindClosestTarget", "Damage.FindClosestNonDeadTarget", "Damage.FindPlayerTarget"):
                for _, arguments in _calls(combined, call_name):
                    radial_ranges.append(_resolved_numeric(arguments[1], tier, combined) if len(arguments) >= 2 else None)
            radial_range = _single(radial_ranges) if radial_ranges and all(value is not None for value in radial_ranges) else None
            if radial_range is not None and radial_range > 0:
                target_shape = "sphere"
                target_dimensions = (0.0, 0.0, radial_range)
            else:
                reasons.append("target range missing, dynamic, or ambiguous")

    waits = [(position, _resolved_numeric(arguments[0], tier, combined)) for position, arguments in _calls(combined, "new WaitForSeconds") if arguments]
    if not damage_positions or any(value is None for _, value in waits):
        reasons.append("hit timing missing or dynamic")
        hit_times: list[float] = []
    else:
        hit_times = []
        elapsed = 0.0
        wait_index = 0
        for damage_position in sorted(damage_positions):
            while wait_index < len(waits) and waits[wait_index][0] < damage_position:
                elapsed += waits[wait_index][1] or 0.0
                wait_index += 1
            hit_times.append(round(elapsed, 6))
        hit_times = list(dict.fromkeys(hit_times))
    if not hit_times:
        reasons.append("no statically timed damage event")
    elif any(current < previous for previous, current in zip(hit_times, hit_times[1:])):
        reasons.append("conditional or parallel hit timing is ambiguous")

    if _has_projectile_delivery(combined):
        reasons.append("projectile/effect instantiation requires referenced component source")
    if reasons:
        return None, sorted(set(reasons))

    width, height, range_value = target_dimensions
    entry = dict(DEFAULT_COMBAT_FIELDS)
    entry.update({
        "id": _runtime_skill_id(character_id, effect_key, tier),
        "character_id": character_id,
        "name_key": f"skill.legacy.{character_id}.{effect_key.lower()}.name",
        "effect_key": effect_key,
        "animation_clip": animation,
        "damage": int(damage),
        "cooldown_seconds": cooldown,
        "hit_delay_seconds": hit_times[0],
        "hit_count": len(hit_times),
        "hit_times_seconds": hit_times,
        "action_duration_seconds": max(hit_times),
        "resource_cost": tree_entry["cost_mp"] or tree_entry["cost_mana"],
        "range": range_value,
        "target_shape": target_shape,
        "target_width": width,
        "target_height": height,
        "max_targets": 0 if "while (" in combined else 1,
    })
    return entry, []


def extract_combat(tree_entries: list[dict[str, Any]], source_root: Path) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    combat_entries: list[dict[str, Any]] = []
    unresolved: list[dict[str, Any]] = []
    sources = {species.lower(): (source_root / f"{species}.cs").read_text(encoding="utf-8-sig") for species in SPECIES}
    grouped: dict[tuple[str, str], list[dict[str, Any]]] = {}
    for tree_entry in tree_entries:
        if tree_entry["effect_key"]:
            grouped.setdefault((tree_entry["character_id"], tree_entry["effect_key"]), []).append(tree_entry)
    for (character_id, effect_key), variants in grouped.items():
        failures: list[list[str]] = []
        for tree_entry in variants:
            entry, reasons = _combat_entry(character_id, tree_entry, sources[character_id])
            if entry is not None:
                combat_entries.append(entry)
                break
            failures.append(reasons)
        else:
            unresolved.append({
                "character_id": character_id,
                "effect_key": effect_key,
                "source_skill_ids": [variant["id"] for variant in variants],
                "patterns": sorted(set.intersection(*(set(reasons) for reasons in failures))),
            })
    return combat_entries, unresolved


def _validate_outputs(tree_entries: list[dict[str, Any]], combat_entries: list[dict[str, Any]], character_ids: set[str]) -> list[dict[str, Any]]:
    for label, entries in (("skill tree", tree_entries), ("combat", combat_entries)):
        ids = [entry["id"] for entry in entries]
        if len(ids) != len(set(ids)):
            raise ExtractionError(f"duplicate IDs in {label}")
        unknown = sorted({entry["character_id"] for entry in entries if entry["character_id"]} - character_ids)
        if unknown:
            raise ExtractionError(f"unknown character IDs in {label}: {unknown}")
    combat_effects = {(entry["character_id"], entry["effect_key"]) for entry in combat_entries}
    return [
        {"character_id": entry["character_id"], "id": entry["id"], "effect_key": entry["effect_key"]}
        for entry in tree_entries
        if entry["effect_key"] and (entry["character_id"], entry["effect_key"]) not in combat_effects
    ]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source_root", type=Path)
    parser.add_argument("--content-dir", type=Path, default=Path("content/v1"))
    parser.add_argument("--unresolved", type=Path, default=Path("artifacts/skills-unresolved.json"))
    arguments = parser.parse_args(argv)

    characters = json.loads((arguments.content_dir / "characters.json").read_text(encoding="utf-8"))
    character_ids = {entry["id"] for entry in characters["characters"]}
    expected = {species.lower() for species in SPECIES}
    if character_ids != expected:
        raise ExtractionError(f"characters.json species mismatch: {sorted(character_ids ^ expected)}")

    existing_skills_path = arguments.content_dir / "skills.json"
    preserved_entries: list[dict[str, Any]] = []
    if existing_skills_path.exists():
        existing_document = json.loads(existing_skills_path.read_text(encoding="utf-8"))
        if existing_document.get("schema_version") == 1 and isinstance(existing_document.get("skills"), list):
            for raw in existing_document["skills"]:
                if raw.get("effect_key"):
                    continue
                preserved = dict(raw)
                preserved.setdefault("effect_key", "")
                preserved_entries.append(preserved)

    tree_entries = extract_skill_tree(arguments.source_root)
    extracted_entries, unresolved = extract_combat(tree_entries, arguments.source_root)
    preserved_ids = {entry["id"] for entry in preserved_entries}
    combat_entries = preserved_entries + [entry for entry in extracted_entries if entry["id"] not in preserved_ids]
    mismatches = _validate_outputs(tree_entries, combat_entries, character_ids)

    arguments.content_dir.mkdir(parents=True, exist_ok=True)
    arguments.unresolved.parent.mkdir(parents=True, exist_ok=True)
    (arguments.content_dir / "skill_tree.json").write_text(
        json.dumps({"schema_version": 1, "skills": tree_entries}, indent=2, ensure_ascii=True) + "\n",
        encoding="utf-8",
    )
    (arguments.content_dir / "skills.json").write_text(
        json.dumps({"schema_version": 1, "skills": combat_entries}, indent=2, ensure_ascii=True) + "\n",
        encoding="utf-8",
    )
    arguments.unresolved.write_text(
        json.dumps({"schema_version": 1, "unresolved": unresolved, "effect_key_mismatches": mismatches}, indent=2, ensure_ascii=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps({
        "tree_entries": len(tree_entries),
        "combat_entries": len(extracted_entries),
        "preserved_runtime_entries": len(preserved_entries),
        "output_combat_entries": len(combat_entries),
        "unresolved": len(unresolved),
        "effect_key_mismatches": len(mismatches),
    }, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())