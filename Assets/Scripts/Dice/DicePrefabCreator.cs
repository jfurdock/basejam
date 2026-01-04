using UnityEngine;

namespace MLBShowdown.Dice
{
    public static class DicePrefabCreator
    {
        public static GameObject CreateD20Prefab()
        {
            GameObject dice = new GameObject("D20");
            
            // Add mesh components
            var d20Mesh = dice.AddComponent<D20Mesh>();
            
            // Add physics
            var rigidbody = dice.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.linearDamping = 0.5f;
            rigidbody.angularDamping = 0.5f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Add collider (sphere approximation for D20)
            var collider = dice.AddComponent<SphereCollider>();
            collider.radius = 0.5f;

            // Create physics material
            PhysicsMaterial diceMat = new PhysicsMaterial("DiceMaterial");
            diceMat.bounciness = 0.4f;
            diceMat.dynamicFriction = 0.4f;
            diceMat.staticFriction = 0.5f;
            diceMat.bounceCombine = PhysicsMaterialCombine.Average;
            diceMat.frictionCombine = PhysicsMaterialCombine.Average;
            collider.material = diceMat;

            // Add visualizer
            dice.AddComponent<DiceVisualizer>();

            return dice;
        }

        public static GameObject CreateDiceRollerSetup()
        {
            // Create parent object
            GameObject diceSystem = new GameObject("DiceSystem");

            // Add dice physics setup (table and walls)
            var physicsSetup = diceSystem.AddComponent<DicePhysicsSetup>();

            // Create dice roller child
            GameObject rollerObj = new GameObject("DiceRoller");
            rollerObj.transform.SetParent(diceSystem.transform);
            rollerObj.transform.localPosition = new Vector3(0, 3, 0);
            
            var diceRoller = rollerObj.AddComponent<DiceRoller3D>();

            // Create spawn point
            GameObject spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.SetParent(rollerObj.transform);
            spawnPoint.transform.localPosition = new Vector3(0, 2, 0);

            // Create dice container
            GameObject diceContainer = new GameObject("DiceContainer");
            diceContainer.transform.SetParent(rollerObj.transform);

            return diceSystem;
        }
    }
}
