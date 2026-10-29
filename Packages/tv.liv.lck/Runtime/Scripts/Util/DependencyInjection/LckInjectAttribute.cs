using System;
using UnityEngine;

namespace Liv.Lck.DependencyInjection
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class InjectLckAttribute : PropertyAttribute { }
}