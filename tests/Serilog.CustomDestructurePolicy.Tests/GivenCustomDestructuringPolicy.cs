using System;
using NUnit.Framework;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Serilog.CustomDestructurePolicy.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class GivenCustomDestructuringPolicy
    {
        [Test]
        public void WhenShouldApplySetButNotFormatterThenItWillThrowArgumentException()
        {
            var exception =
                Assert.Throws<ArgumentException>(() =>
                    new CustomPolicyOptions().AddPolicy("error", (_, _) => true));

            Assert.That(exception!.ParamName, Is.EqualTo("formatter"));
            Assert.That(exception.Message, Does.StartWith("Formatter must be defined when should apply defined."));
        }

        [Test]
        public void WhenFormatterSetButNotShouldApplyThenItWillThrowArgumentException()
        {
            var exception =
                Assert.Throws<ArgumentException>(() =>
                    new CustomPolicyOptions().AddPolicy("error", formatter: _ => "dummy"));

            Assert.That(exception!.ParamName, Is.EqualTo("shouldApply"));
            Assert.That(exception.Message, Does.StartWith("Should apply must be defined when formatter defined."));
        }

        [Test]
        public void WhenFilteredByOneOfExcludeCallbackThenItWillNotBeIncluded()
        {
            var options = new CustomPolicyOptions()
                .AddPolicy("exclude non writable", isExcluded: p => !p.CanWrite)
                .AddPolicy("exclude field", isExcluded: p => p.GetMethod == null);
            var events = new List<LogEvent>();
            var logger = new LoggerConfiguration()
                .Destructure.With(new CustomRuleDestructuringPolicy(options))
                .WriteTo.Sink(new DelegatingSink(events.Add))
                .CreateLogger();

            var sut = new Test { Name = "my name" };
            logger.Information("Hello {@Data}", sut);

            Assert.That(events.Count, Is.EqualTo(1));
            var logEvent = events[0];
            Assert.That(logEvent.Properties, Does.ContainKey("Data"));
            Assert.That(logEvent.Properties["Data"], Is.TypeOf<StructureValue>());
            var logProperties = ((StructureValue)logEvent.Properties["Data"]).Properties;
            Assert.That(logProperties.Select(p => p.Name),
                Is.Not.SupersetOf(new[] { nameof(Test.ReadOnly), nameof(Test.Default) }).And.Contain(nameof(Test.Name)));
            var property = logProperties.First(p => p.Name == nameof(Test.Name));
            Assert.That(property.Value.ToString(), Is.EqualTo($"\"{sut.Name}\""));
        }

        [Test]
        public void WhenApplyingCustomFormatThenItWillBuildResultFromFactory()
        {
            var options = new CustomPolicyOptions()
                .AddPolicy("add hello", IsStringNamePropertyName, value => $"Hello {value}!")
                .AddPolicy("add welcome", IsStringNamePropertyName, value => $"{value} Welcome home.");
            var events = new List<LogEvent>();
            var logger = new LoggerConfiguration()
                .Destructure.With(new CustomRuleDestructuringPolicy(options))
                .WriteTo.Sink(new DelegatingSink(events.Add))
                .CreateLogger();

            var sut = new Test { Name = "world" };
            logger.Information("Hello {@Data}", sut);

            Assert.That(events.Count, Is.EqualTo(1));
            var logEvent = events[0];
            Assert.That(logEvent.Properties, Does.ContainKey("Data"));
            Assert.That(logEvent.Properties["Data"], Is.TypeOf<StructureValue>());
            var logProperties = ((StructureValue)logEvent.Properties["Data"]).Properties;
            var nameProperty = logProperties.FirstOrDefault(p => p.Name == nameof(Test.Name));
            Assert.That(nameProperty, Is.Not.Null);
            Assert.That(nameProperty!.Value.ToString(), Is.EqualTo("\"Hello world! Welcome home.\""));
        }

        [Test]
        public void WhenManyFormattersDefinedThenItWillBeAppliedInOrder()
        {
            var options = new CustomPolicyOptions()
                .AddPolicy("add hello", (p, value) =>
                p.Name == nameof(Test.Name) && value is string word && !string.IsNullOrWhiteSpace(word),
                value => $"Hello {value}!");
            var events = new List<LogEvent>();
            var logger = new LoggerConfiguration()
                .Destructure.With(new CustomRuleDestructuringPolicy(options))
                .WriteTo.Sink(new DelegatingSink(events.Add))
                .CreateLogger();

            var sut = new Test { Name = "world" };
            logger.Information("Hello {@Data}", sut);

            Assert.That(events.Count, Is.EqualTo(1));
            var logEvent = events[0];
            Assert.That(logEvent.Properties, Does.ContainKey("Data"));
            Assert.That(logEvent.Properties["Data"], Is.TypeOf<StructureValue>());
            var logProperties = ((StructureValue)logEvent.Properties["Data"]).Properties;
            var nameProperty = logProperties.FirstOrDefault(p => p.Name == nameof(Test.Name));
            Assert.That(nameProperty, Is.Not.Null);
            Assert.That(nameProperty!.Value.ToString(), Is.EqualTo("\"Hello world!\""));
        }

        [Test]
        public void WhenDestructuringNestedClassThenItWillBePopulatedAccordingly()
        {
            var options = new CustomPolicyOptions().AddPolicy("add hello", IsStringNamePropertyName, value => $"Hello {value}!");
            var events = new List<LogEvent>();
            var logger = new LoggerConfiguration()
                .Destructure.With(new CustomRuleDestructuringPolicy(options))
                .WriteTo.Sink(new DelegatingSink(events.Add))
                .CreateLogger();

            var sut = new Nested
            {
                HasInner = true,
                Child =
                {
                    Name = "me"
                }
            };
            logger.Information("Hello {@Data}", sut);

            Assert.That(events.Count, Is.EqualTo(1));
            var logEvent = events[0];
            Assert.That(logEvent.Properties, Does.ContainKey("Data"));
            Assert.That(logEvent.Properties["Data"], Is.TypeOf<StructureValue>());
            var logProperties = ((StructureValue)logEvent.Properties["Data"]).Properties;
            AssertLogProperty(logProperties, nameof(Nested.HasInner), true.ToString());
            var childProperty = logProperties.FirstOrDefault(p => p.Name == nameof(Nested.Child));
            Assert.That(childProperty, Is.Not.Null);
            Assert.That(childProperty!.Value, Is.TypeOf<StructureValue>());
            var properties = ((StructureValue)childProperty.Value).Properties;
            AssertLogProperty(properties, nameof(Test.ReadOnly), "\"never changed\"");
            AssertLogProperty(properties, nameof(Test.Default), "\"hello\"");
            AssertLogProperty(properties, nameof(Test.Name), "\"Hello me!\"");
        }

        [Test]
        public void WhenMaskedAndCustomMaskingSpecifiedThenItWillBeReplacedAsConfigured()
        {
            const string maskingText = "-";
            var events = new List<LogEvent>();
            var logger = new LoggerConfiguration()
                .Destructure.ByCustomRules(rule => rule.Masking(maskingText, new[] { nameof(Test.Name) }))
                .WriteTo.Sink(new DelegatingSink(events.Add))
                .CreateLogger();

            var sut = new Test { Name = "secret", Default = "world" };
            logger.Information("Hello {@Data}", sut);

            Assert.That(events.Count, Is.EqualTo(1));
            var logEvent = events[0];
            Assert.That(logEvent.Properties, Does.ContainKey("Data"));
            Assert.That(logEvent.Properties["Data"], Is.TypeOf<StructureValue>());
            var properties = ((StructureValue)logEvent.Properties["Data"]).Properties;
            AssertLogProperty(properties, nameof(Test.ReadOnly), "\"never changed\"");
            AssertLogProperty(properties, nameof(Test.Default), "\"world\"");
            AssertLogProperty(properties, nameof(Test.Name), $"\"{maskingText}\"");
        }

        [Test]
        public void WhenMaskedWithDefaultTextThenItWillBeReplacedByFiveAsterisks()
        {
            var events = new List<LogEvent>();
            var logger = new LoggerConfiguration()
                .Destructure.ByCustomRules(rule => rule.Masking(new[] { nameof(Test.Name) }))
                .WriteTo.Sink(new DelegatingSink(events.Add))
                .CreateLogger();

            var sut = new Test { Name = "secret", Default = "world" };
            logger.Information("Hello {@Data}", sut);

            Assert.That(events.Count, Is.EqualTo(1));
            var logEvent = events[0];
            Assert.That(logEvent.Properties, Does.ContainKey("Data"));
            Assert.That(logEvent.Properties["Data"], Is.TypeOf<StructureValue>());
            var properties = ((StructureValue)logEvent.Properties["Data"]).Properties;
            AssertLogProperty(properties, nameof(Test.ReadOnly), "\"never changed\"");
            AssertLogProperty(properties, nameof(Test.Default), "\"world\"");
            AssertLogProperty(properties, nameof(Test.Name), "\"*****\"");
        }

        private static void AssertLogProperty(IReadOnlyList<LogEventProperty> logProperties, string propertyName,
            string expected)
        {
            var hasInnerProperty = logProperties.FirstOrDefault(p => p.Name == propertyName);
            Assert.That(hasInnerProperty, Is.Not.Null);
            Assert.That(hasInnerProperty!.Value.ToString(), Is.EqualTo(expected));
        }

        private bool IsStringNamePropertyName(PropertyInfo p, object value) =>
            p.Name == nameof(Test.Name) && value is string word && !string.IsNullOrWhiteSpace(word);

        private class Test
        {
            public string ReadOnly { get; } = "never changed";
            public string Default { get; set; } = "hello";
            public string Name { get; set; } = string.Empty;
        }

        private class Nested
        {
            public bool HasInner { get; set; }
            public Test Child { get; } = new();
        }
    }
}
