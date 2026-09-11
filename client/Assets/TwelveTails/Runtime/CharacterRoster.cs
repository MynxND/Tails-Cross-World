using System;
using UnityEngine;

namespace TwelveTails.Gameplay
{
    public static class CharacterRoster
    {
        public static readonly string[] Ids =
        {
            "wolf", "bison", "panda", "whale", "mole", "rabbit",
            "monkey", "sheep", "penguin", "bat", "chameleon", "cat"
        };

        public static readonly string[] Classes =
        {
            "Swordmaster", "Berserker", "Martial Artist", "Guardian", "Bomber", "Alchemist",
            "Summoner", "Priest", "Ice Wizard", "Night Mage", "Archer", "Treasure Hunter"
        };

        public static int IndexOf(string id)
        {
            var index = Array.IndexOf(Ids, id);
            return index < 0 ? 0 : index;
        }
    }
}
