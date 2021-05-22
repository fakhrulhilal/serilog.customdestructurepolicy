# Serilog.CustomDestructurePolicy
![Nuget](https://img.shields.io/nuget/v/Serilog.CustomDestructurePolicy?style=flat-square) ![Nuget](https://img.shields.io/nuget/dt/Serilog.CustomDestructurePolicy?color=blue&style=flat-square)

Custom destructuring policy for serilog. What you can custom for destructuring:

1. Ignore property by certain rule
2. Mutate output value by certain rule

And currently it has builtin rule, you just need to configure during creating logger:

1. Ignore static property
2. Ignore compiler generated property
3. Mask property, useful for hide sensitive data like password
4. Fix for record destructuring



## Installation

Powershell

```powershell
Install-Package Serilog.CustomDestructurePolicy
```

dotnet cli

`dotnet add package Serilog.CustomDestructurePolicy`

## Usage

### Builtin Policies

```csharp
using Serilog.CustomDestructurePolicy;

record User(int Id, string Name, string Password, string Secret)
{
    public static int MaxAge = 60;
}

var logger = new LoggerConfiguration()
	.Destructure.ByCustomRules(rule =>
	{
        // ignore static property
		rule.IgnoreStatic();
        // ignore compiler generated
		rule.IgnoreGeneratedByCompiler();
        // ignore `EqualityContract` property
		rule.ByProperRecordProcessing();
        // replace all property named 'Password' with 3 asterisks (default: 5 asterisks)
		rule.Masking("***", new[] {"Password"});
        // ignore all properties named: Secret, Sensitive
		rule.Excluding("Secret", "Sensitive");
	}).CreateLogger();

logger.Information("User data {@User}", new User(1, "world", "my_password", "confidental"));
// printing:
// User data { Id: 1, Name: "world", Password: "***" }
```

### Custom Policy

Applying custom rule is the main feature of this extension. It can be combined with builtin policy as well. To exclude certain property matching rule, you can use this:

```csharp
var logger = new LoggerConfiguration()
	.Destructure.ByCustomRules(rule =>
	{
        // ignore readonly property
		rule.AddPolicy("exclude readonly", isExcluded: p => !p.CanWrite)
        // apply builtin policies
		rule.IgnoreGeneratedByCompiler()
            .ByProperRecordProcessing()
            .Masking("***", new[] {"Password"});
	}).CreateLogger();
```

or you can mutate the value like follows:

```csharp
// append 'Hello' for all 'Name' property which is not empty
rule.AddPolicy("add hello", (p, originalValue) =>
p.Name == nameof(Test.Name) && originalValue is string word && !string.IsNullOrWhiteSpace(word), originalValue => $"Hello {value}!");
```

This mutations are passing value from previous policy. So it can be combined like follows:

```csharp
bool IsStringNamePropertyName(PropertyInfo p, object value) => p.Name == nameof(Test.Name) && value is string word && !string.IsNullOrWhiteSpace(word);
rule
    // append 'Hello' for all 'Name' property which is not empty
    .AddPolicy("add hello", IsStringNamePropertyName, originalValue => $"Hello {originalValue}!")
    // add welcome message
    .AddPolicy("add welcome", IsStringNamePropertyName, originalValue => $"{originalValue} Welcome home");

// prints:
// User data { Id: 1, Name: "Hello world! Welcome home", Password: "my_password", Secret: "confidental" }
```
In case there is issue (such as the process stops at logging), you can enable debug mode using `SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine(msg));`. This is useful to trace problem. This extension logs which rule excludes or apply certain formatting.

## Inspiration

This extension is inspired by [masking.serilog](https://github.com/evjenio/masking.serilog) which issue with [record destructuring](https://github.com/evjenio/masking.serilog/issues/5) and the issue hasn't been resolved at the moment. Unfortunately, serilog doesn't support [multiple destructuring](https://github.com/serilog/serilog/issues/1581). It means, which destructor configured first, that policy will be applied. So creating new destructure policy extension will not solve the problem. Thus this extension create, the main goal of this extension is making as customizable as possible.