using UnityEngine;

namespace TwelveTails.Gameplay
{
    public static class ProceduralCharacter
    {
        private static readonly Color[] Colors =
        {
            new(0.38f, 0.45f, 0.55f), new(0.40f, 0.22f, 0.12f), new(0.92f, 0.92f, 0.86f),
            new(0.32f, 0.68f, 0.82f), new(0.35f, 0.25f, 0.18f), new(0.93f, 0.84f, 0.72f),
            new(0.62f, 0.36f, 0.16f), new(0.95f, 0.92f, 0.78f), new(0.10f, 0.18f, 0.25f),
            new(0.30f, 0.16f, 0.40f), new(0.30f, 0.70f, 0.28f), new(0.82f, 0.48f, 0.22f)
        };

        public static GameObject Create(string characterId, Transform parent)
        {
            var index = CharacterRoster.IndexOf(characterId);
            var root = new GameObject("Character Visual");
            root.transform.SetParent(parent, false);
            var color = Colors[index];
            Part(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0, 0, 0), new Vector3(.75f, .72f, .65f), color);
            Part(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0, .92f, .05f), new Vector3(.82f, .72f, .76f), color);
            Part(root.transform, PrimitiveType.Sphere, "Muzzle", new Vector3(0, .78f, .45f), new Vector3(.38f, .25f, .3f), Color.Lerp(color, Color.white, .35f));
            Eye(root.transform, -.18f); Eye(root.transform, .18f);
            AddSpeciesParts(root.transform, index, color);
            AddClassProp(root.transform, index);
            return root;
        }

        private static void AddSpeciesParts(Transform root, int index, Color color)
        {
            if (index == 3) // whale
            {
                Part(root, PrimitiveType.Cube, "Tail", new Vector3(0, .2f, -.55f), new Vector3(.65f, .12f, .45f), color, new Vector3(35, 0, 0));
                Part(root, PrimitiveType.Sphere, "Fin", new Vector3(0, .95f, .55f), new Vector3(.15f, .35f, .15f), color);
                return;
            }
            if (index == 8) // penguin
            {
                Part(root, PrimitiveType.Sphere, "Belly", new Vector3(0, .05f, .42f), new Vector3(.48f, .7f, .12f), Color.white);
                Part(root, PrimitiveType.Cube, "Beak", new Vector3(0, .88f, .58f), new Vector3(.28f, .12f, .3f), new Color(1f, .62f, .08f));
                return;
            }
            if (index == 9) // bat
            {
                Wing(root, -1); Wing(root, 1);
                Ear(root, -.27f, 1.55f, .18f, color); Ear(root, .27f, 1.55f, .18f, color);
                return;
            }
            if (index == 1) // bison
            {
                Horn(root, -.38f, -25); Horn(root, .38f, 25);
                Part(root, PrimitiveType.Sphere, "Mane", new Vector3(0, .55f, 0), new Vector3(1.05f, .55f, .8f), Color.Lerp(color, Color.black, .3f));
                return;
            }
            if (index == 5) // rabbit
            {
                Ear(root, -.22f, 1.72f, .22f, color); Ear(root, .22f, 1.72f, .22f, color);
                Part(root, PrimitiveType.Sphere, "Tail", new Vector3(0, .25f, -.55f), Vector3.one * .28f, Color.white);
                return;
            }
            if (index == 10) // chameleon
            {
                Part(root, PrimitiveType.Sphere, "Eye L", new Vector3(-.38f, 1.02f, .1f), Vector3.one * .23f, color);
                Part(root, PrimitiveType.Sphere, "Eye R", new Vector3(.38f, 1.02f, .1f), Vector3.one * .23f, color);
                Part(root, PrimitiveType.Sphere, "Tail Curl", new Vector3(0, .25f, -.55f), Vector3.one * .35f, color);
                return;
            }
            var earHeight = index == 0 || index == 11 ? 1.52f : 1.45f;
            Ear(root, -.27f, earHeight, index == 7 ? .3f : .22f, color);
            Ear(root, .27f, earHeight, index == 7 ? .3f : .22f, color);
            if (index == 2)
            {
                Part(root, PrimitiveType.Sphere, "Patch L", new Vector3(-.2f, 1.02f, .34f), new Vector3(.24f, .3f, .08f), Color.black);
                Part(root, PrimitiveType.Sphere, "Patch R", new Vector3(.2f, 1.02f, .34f), new Vector3(.24f, .3f, .08f), Color.black);
            }
            if (index == 6 || index == 11 || index == 0)
                Part(root, PrimitiveType.Capsule, "Tail", new Vector3(.38f, .2f, -.48f), new Vector3(.18f, .55f, .18f), color, new Vector3(55, 0, -25));
        }

        private static void AddClassProp(Transform root, int index)
        {
            var propColor = new Color(.22f, .25f, .3f);
            if (index <= 2 || index == 11)
                Part(root, PrimitiveType.Cube, "Class Prop", new Vector3(.58f, .35f, .05f), new Vector3(.12f, 1.1f, .12f), propColor, new Vector3(0, 0, -18));
            else if (index == 5 || index == 7 || index == 8 || index == 9)
                Part(root, PrimitiveType.Cylinder, "Class Prop", new Vector3(.58f, .35f, .05f), new Vector3(.12f, .65f, .12f), propColor, new Vector3(0, 0, -12));
            else
                Part(root, PrimitiveType.Sphere, "Class Prop", new Vector3(.58f, .35f, .05f), Vector3.one * .25f, propColor);
        }

        private static void Eye(Transform root, float x) =>
            Part(root, PrimitiveType.Sphere, "Eye", new Vector3(x, 1.03f, .38f), Vector3.one * .12f, Color.black);

        private static void Ear(Transform root, float x, float y, float width, Color color) =>
            Part(root, PrimitiveType.Capsule, "Ear", new Vector3(x, y, 0), new Vector3(width, .5f, width), color);

        private static void Horn(Transform root, float x, float angle) =>
            Part(root, PrimitiveType.Cylinder, "Horn", new Vector3(x, 1.35f, 0), new Vector3(.11f, .35f, .11f), new Color(.85f, .72f, .48f), new Vector3(0, 0, angle));

        private static void Wing(Transform root, int side) =>
            Part(root, PrimitiveType.Cube, "Wing", new Vector3(.65f * side, .55f, 0), new Vector3(.7f, .08f, .65f), new Color(.22f, .1f, .3f), new Vector3(0, 0, -20 * side));

        private static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color, Vector3 rotation = default)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localEulerAngles = rotation;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            part.GetComponent<Renderer>().material = material;
            return part;
        }
    }
}
