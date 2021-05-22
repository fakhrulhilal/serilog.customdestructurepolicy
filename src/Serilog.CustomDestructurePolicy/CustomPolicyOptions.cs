using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Serilog.CustomDestructurePolicy
{
    /// <summary>
    /// Options for building custom destructuring rules
    /// </summary>
    public class CustomPolicyOptions
    {
        internal List<Rule> Rules { get; } = new();

        /// <summary>
        /// Add custom policy rule
        /// </summary>
        /// <param name="name">Rule name for debugging purpose</param>
        /// <param name="shouldApply">
        /// When this is true, the it will apply custom <paramref name="formatter"/>.
        /// First param is property info and second param is the original value.
        /// </param>
        /// <param name="formatter">
        /// Applied custom format when <paramref name="shouldApply"/> is true.
        /// The parameter is the original value.
        /// </param>
        /// <param name="isExcluded">Exclude certain property when returning <c>true</c></param>
        /// <returns></returns>
        public CustomPolicyOptions AddPolicy(string name, Func<PropertyInfo, object, bool>? shouldApply = null,
            Func<object, object?>? formatter = null, Func<PropertyInfo, bool>? isExcluded = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException(nameof(name));
            if (shouldApply != null && formatter == null)
                throw new ArgumentException("Formatter must be defined when should apply defined.", nameof(formatter));
            if (shouldApply == null && formatter != null)
                throw new ArgumentException("Should apply must be defined when formatter defined.", nameof(shouldApply));
            var rule = new Rule(name);
            if (isExcluded != null) rule.IsExcluded = isExcluded;
            if (shouldApply != null && formatter != null)
            {
                rule.ShouldApply = shouldApply;
                rule.Format = formatter;
            }
            Rules.Add(rule);
            return this;
        }

        internal PropertyInfo[] GetProperties(TypeInfo type, Action<Rule, PropertyInfo>? logging = null)
        {
            var output = new List<PropertyInfo>();
            foreach (var propertyInfo in type.GetRuntimeProperties().Where(p => p.CanRead))
            {
                var excludingRule = Rules.FirstOrDefault(rule => rule.IsExcluded(propertyInfo));
                if (excludingRule != null)
                {
                    logging?.Invoke(excludingRule, propertyInfo);
                    continue;
                }
                output.Add(propertyInfo);
            }

            return output.ToArray();
        }

        internal class Rule
        {
            internal Rule(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentNullException(nameof(name));
                Name = name;
            }

            public string Name { get; }

            /// <summary>
            /// When this is true, then it will apply custom <see cref="Format"/>,
            /// otherwise, it will follow normal formatter.
            /// Callback parameters:
            /// <list type="">
            /// </list>
            /// </summary>
            public Func<PropertyInfo, object, bool> ShouldApply { get; internal set; } = (_, _) => false;

            /// <summary>
            /// When this is true, then it will be excluded and no custom <see cref="Format"/> wil be applied
            /// </summary>
            public Func<PropertyInfo, bool> IsExcluded { get; internal set; } = _ => false;

            /// <summary>
            /// Custom formatter when <see cref="ShouldApply"/> is true
            /// </summary>
            public Func<object, object?> Format { get; internal set; } = o => o;
        }
    }
}
