using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace Serilog.CustomDestructurePolicy.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class GivenCustomPolicyOptionsExtensions
    {
        [Test]
        public void WhenListedInExcludedListThenItWillNotBeIncluded()
        {
            string message = string.Empty;
            var options = new CustomPolicyOptions().Excluding(nameof(Test.Name));

            var result = options.GetProperties(typeof(Test).GetTypeInfo(),
                (rule, p) => message = $"Property {p.Name} is excluded by {rule.Name} rule");

            Assert.That(result.Select(p => p.Name), Does.Not.Contain(nameof(Test.Name)));
            Assert.That(message, Is.EqualTo($"Property {nameof(Test.Name)} is excluded by excluding rule"));
        }

        [Test]
        public void WhenNotListedInExcludedListThenItWillBeIncluded()
        {
            var options = new CustomPolicyOptions().Excluding(nameof(Test.Name));

            var result = options.GetProperties(typeof(Test).GetTypeInfo());

            Assert.That(result.Select(p => p.Name),
                Is.EquivalentTo(new[] {nameof(Test.Generated), nameof(Test.Static)}));
        }

        [Test]
        public void WhenMaskedByDefaultWordThenItWillBeIncluded()
        {
            var options = new CustomPolicyOptions().Masking(new[] {nameof(Test.Name)});

            var result = options.GetProperties(typeof(Test).GetTypeInfo());

            Assert.That(result.Select(p => p.Name), Does.Contain(nameof(Test.Name)));
        }

        [Test]
        public void WhenApplyingRecordPolicyThenItWillNotIncludeEqualityContract()
        {
            string message = string.Empty;
            var options = new CustomPolicyOptions().ByProperRecordProcessing();

            var result = options.GetProperties(new TestRecord(1, "hello").GetType().GetTypeInfo(),
                (rule, p) => message = $"Property {p.Name} is excluded by {rule.Name} rule");

            Assert.That(result.Select(p => p.Name), Does.Not.Contain("EqualityContract"));
            Assert.That(message, Is.EqualTo("Property EqualityContract is excluded by record processing rule"));
        }

        [Test]
        public void WhenApplyingRecordPolicyThenAllPublicPropertiesWillBeIncluded()
        {
            var options = new CustomPolicyOptions().ByProperRecordProcessing();

            var result = options.GetProperties(typeof(TestRecord).GetTypeInfo());

            Assert.That(result.Select(p => p.Name),
                Is.EquivalentTo(new[] {nameof(TestRecord.Id), nameof(TestRecord.Name)}));
        }

        [Test]
        public void WhenIgnoringStaticPropertyThenItWillNotBeIncluded()
        {
            string message = string.Empty;
            var options = new CustomPolicyOptions().IgnoreStatic();

            var result = options.GetProperties(typeof(Test).GetTypeInfo(),
                (rule, p) => message = $"Property {p.Name} is excluded by {rule.Name} rule");

            Assert.That(result.Select(p => p.Name), Does.Not.Contain(nameof(Test.Static)));
            Assert.That(message, Is.EqualTo($"Property {nameof(Test.Static)} is excluded by ignore static rule"));
        }

        [Test]
        public void WhenIgnoringStaticPropertyThenNonStaticPropertyWillBeIncluded()
        {
            var options = new CustomPolicyOptions().IgnoreStatic();

            var result = options.GetProperties(typeof(Test).GetTypeInfo());

            Assert.That(result.Select(p => p.Name),
                Is.EquivalentTo(new[] {nameof(Test.Generated), nameof(Test.Name)}));
        }

        [Test]
        public void WhenIgnoringGeneratedByCompilerThenItWillExcludeAllPropertiesDecoratedByCompilerGeneratedAttribute()
        {
            string message = string.Empty;
            var options = new CustomPolicyOptions().IgnoreGeneratedByCompiler();

            var result = options.GetProperties(typeof(Test).GetTypeInfo(),
                (rule, p) => message = $"Property {p.Name} is excluded by {rule.Name} rule");

            Assert.That(result.Select(p => p.Name), Does.Not.Contain(nameof(Test.Generated)));
            Assert.That(message, Is.EqualTo($"Property {nameof(Test.Generated)} is excluded by ignore generated by compiler rule"));
        }

        [Test]
        public void WhenIgnoringGeneratedByCompilerThenNonDecoratedByCompilerGeneratedAttributeWillBeIncluded()
        {
            var options = new CustomPolicyOptions().IgnoreGeneratedByCompiler();

            var result = options.GetProperties(typeof(Test).GetTypeInfo());

            Assert.That(result.Select(p => p.Name),
                Is.EquivalentTo(new[] {nameof(Test.Static), nameof(Test.Name)}));
        }

        private record TestRecord(int Id, string Name);
        private class Test
        {
            public static string Static { get; } = "never changed";
            [CompilerGenerated] 
            public string Generated { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
        }
    }
}