using Serilog.Configuration;
using Serilog.CustomDestructurePolicy;
using System;

// ReSharper disable once CheckNamespace
namespace Serilog
{
    /// <summary>
    /// Extensions for serilog destructuring config
    /// </summary>
    public static class SerilogExtensions
    {
        /// <summary>
        /// Build custom rule for destructuring
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static LoggerConfiguration ByCustomRules(this LoggerDestructuringConfiguration builder,
            Action<CustomPolicyOptions> configure)
        {
            var rules = new CustomPolicyOptions();
            configure(rules);
            return builder.With(new CustomRuleDestructuringPolicy(rules));
        }

        /// <summary>
        /// Fix destructuring for record type
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static LoggerConfiguration ByCustomRules(this LoggerDestructuringConfiguration builder)
        {
            var rules = new CustomPolicyOptions().ByProperRecordProcessing();
            return builder.With(new CustomRuleDestructuringPolicy(rules));
        }
    }
}
