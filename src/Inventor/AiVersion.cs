﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xarial.XCad.Inventor.Enums;

namespace Xarial.XCad.Inventor
{
    public interface IAiVersion : IXVersion
    {
        AiVersion_e Major { get; }
    }

    internal class AiVersion : IAiVersion
    {
        public AiVersion_e Major { get; }

        public string DisplayName
        {
            get 
            {
                string vers;

                if (Major == AiVersion_e.Inventor5dot3)
                {
                    vers = "5.3";
                }
                else 
                {
                    vers = Major.ToString().Substring("Inventor".Length);
                }

                return $"Inventor {vers}";
            }
        }

        internal AiVersion(AiVersion_e major)
        {
            Major = major;
        }

        public int CompareTo(IXVersion other)
        {
            if (other is IAiVersion)
            {
                return ((int)Major).CompareTo((int)((IAiVersion)other).Major);
            }
            else
            {
                throw new InvalidCastException("Can only compare Inventor versions");
            }
        }

        public override int GetHashCode()
        {
            return (int)Major;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is IAiVersion))
                return false;

            return Equals((IAiVersion)obj);
        }

        public bool Equals(AiVersion other)
            => Major == other.Major;

        public bool Equals(IXVersion other) => Equals((object)other);

        public static bool operator ==(AiVersion version1, AiVersion version2)
            => version1.Equals(version2);

        public static bool operator !=(AiVersion version1, AiVersion version2)
            => !version1.Equals(version2);

        public override string ToString() => DisplayName;
    }
}
