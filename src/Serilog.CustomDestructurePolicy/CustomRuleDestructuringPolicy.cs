using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.CustomDestructurePolicy
{
    /// <summary>
    /// Apply destructuring using one or more policies
    /// </summary>
    public class CustomRuleDestructuringPolicy : IDestructuringPolicy
    {
        private readonly CustomPolicyOptions _options;
        private readonly Dictionary<TypeInfo, PropertyInfo[]> _cache = new();
        private readonly object _sync = new();

        /// <summary>
        /// Initiate new custom destructuring with certain options
        /// </summary>
        /// <param name="options"></param>
        public CustomRuleDestructuringPolicy(CustomPolicyOptions options)
        {
            _options = options;
        }

        /// <inheritdoc />
        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
        {
            var logEventProperties = new List<LogEventProperty>();
            result = null;
            if (value is IEnumerable) return false;

            var type = value.GetType().GetTypeInfo();
            var properties = GetProperties(type);
            foreach (var property in properties)
            {
                object? propertyValue = GetPropertyValue(value, property);
                if (propertyValue is not null)
                {
                    object originalValue = propertyValue;
                    foreach (var rule in _options.Rules.Where(r => r.ShouldApply(property, originalValue)))
                    {
                        SelfLog.WriteLine("Applying rule {0} for property {1}", rule.Name, property);
                        originalValue = rule.Format(originalValue)!;
                    }
                    propertyValue = originalValue;
                }
                var logValue = propertyValue is null
                    ? new ScalarValue(null)
                    : propertyValueFactory.CreatePropertyValue(propertyValue, true);
                logEventProperties.Add(new LogEventProperty(property.Name, logValue));
            }

            result = new StructureValue(logEventProperties);
            return true;
        }

        private PropertyInfo[] GetProperties(TypeInfo type)
        {
            lock (_sync)
                if (_cache.TryGetValue(type, out var result))
                    return result;
            var properties = _options.GetProperties(type, (rule, property) =>
                SelfLog.WriteLine($"Property '{property.Name}' is excluded by '{rule.Name}' rule."));
            var output = properties.ToArray();
            lock (_sync) _cache[type] = output;
            return output;
        }

        private object? GetPropertyValue(object value, PropertyInfo property)
        {
            try
            {
                if (property.GetIndexParameters().Length > 0) return null;
                return property.GetValue(value);
            }
            catch (TargetInvocationException exc)
            {
                SelfLog.WriteLine("The property accessor {0} threw exception {1}", property, exc);
                string? exceptionName = exc.InnerException?.GetType().FullName ?? exc.GetType().FullName;
                return $"Type property accessor '{property.Name}' threw an exception: {exceptionName}";
            }
        }
    }
}
