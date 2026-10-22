using System.Numerics;
using UnityEngine;

namespace BugArena
{
    public sealed class EnergyOrb
    {
        public Fighter Owner, Target;
        public UnityEngine.Vector2 Position;
        public BigInteger Damage = new BigInteger(24);
        public BigInteger Reflections;
        public const float Speed = 4.8f;
        public const float ContactRadius = 1.35f;
        public string DamageLabel {
            get { var digits = Damage.ToString(); return digits.Length <= 7 ? digits : digits[0] + "." + digits.Substring(1, 2) + "e" + (digits.Length - 1); }
        }
        public void Reflect(Fighter defender, Fighter opponent)
        { Owner = defender; Target = opponent; Damage *= 2; Reflections++; }
    }
    public struct OrbEvent
    {
        public Fighter Actor;
        public UnityEngine.Vector2 Position;
        public bool Reflected;
        public string Damage;
        public BigInteger Returns;
    }
}
