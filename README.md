# Discriminated Unions
## Overview
This library contains a source generator for creating [Discriminated Unions](https://en.wikipedia.org/wiki/Tagged_union) in C# (also known as tagged unions, variants or choice types).

The generated unions are meant to be more robust than other techniques used to build discriminated unions in C#, e.g. subclassing an abstract class or record. The problem with these is that they are open-ended and the compiler does not lend us enough help to perform exhaustive matching. Exhaustive matching exists for plain enums, but 1) exhaustiveness is a suggestion, not a requirement, and 2) different editors suggest exhaustiveness differently. This library ensures that all cases have been accounted for when matching against the union's case.

The generated union is also a `struct`, meaning that it cannot be null. This is further enforced by an analyzer that makes sure neither `new` or `default` is going to work and a union can only be instantiated by explicitly selecting a case.

Note that the union cases can not be pattern matched against, which is a downside.

## Important notes
Because C# source generators are a relatively new concept it is important to make sure your IDE is up to date. While developing this library I got the strangest errors because my editor was not fully updated at first.

There also appears to be some sort of IDE caching bug in which sources are not properly generated as they should without reloading the project if using Jetbrains Rider, or a full IDE restart if using Visual Studio. This is very annoying but important to keep in mind after having created, updated or moved a union declaration. You don't necessarily implement union types that often and their use may very well warrant dealing with this annoyance for now.

## Usage

A union will only be generated when these criteria are met:
- The type is a `partial` `struct`
- The type is marked with the `[DiscriminatedUnion]` attribute from the `nemuikoneko.DiscriminatedUnions` namespace
- The type contains an interface named `Cases` whose members are of the format `void CaseName(<arguments>);`
- There exists at least one union case

Note that no error will be reported if these criteria are not met; the union will simply not be generated.
The generated union will exist as a `partial` type in a generated file and can be inspected by pressing F12 in the IDE.

### Basic union template
```csharp
[DiscriminatedUnion]
partial struct MyUnion
{
    interface Cases
    {
        void FirstCase();
        void SecondCase(int n);
        void ThirdCase(string s, int n);
    }
}

var union1 = MyUnion.FirstCase;
var union2 = MyUnion.SecondCase(123);
var union3 = MyUnion.ThirdCase("s", 123);
```

The union is instantiated by calling its static properties/methods dependent on which case to instantiate.

### Union comparison
Unions can be compared and will be equal when their cases and data match:
```csharp
var union1 = MyUnion.ThirdCase("a", 1);
var union2 = MyUnion.ThirdCase("a", 1);
var union3 = MyUnion.ThirdCase("b", 1);
var union4 = MyUnion.SecondCase(2);

Console.WriteLine(union1 == union2); // True
Console.WriteLine(union1 == union3); // False
Console.WriteLine(union1 == union4); // False
```

### Generic union
```csharp
[DiscriminatedUnion]
partial struct Option<T>
{
    interface Cases
    {
        void None();
        void Some(T obj);
    }
}

var name1 = Option<string>.Some("Bob");
var name2 = Option<string>.None;
```

### Matching
What differentiates a discriminated union from a plain enum or hierarchical structure is the ability to ensure exhaustive matching.

Each generated union comes with a `Match` method that ensures all cases have been accounted for:
```csharp
[DiscriminatedUnion]
partial struct ParseError
{
    interface Cases
    {
        void TooShort();
        void TooLong();
        void IllegalSymbol(char illegalSymbol);
    }
}

var parseError = ParseError.TooShort;
var readableErrorMessage = parseError.Match(
    TooShort: () => "Too short!", // This lambda will be called since it matches the active case
    TooLong: () => "Too long!",
    IllegalSymbol: x => $"Illegal symbol: {x}!");
```

Whenever a union case is removed, renamed, added or in any way updated, all match expressions like this will no longer compile, keeping your code safe from becoming outdated.

It is optional to specify the case name, but it is highly recommended to do so as it makes the code much more readable.
It is also optional to use the lambda parameters, that is to say it would be possible to ignore the illegal symbol `x` here by replacing it with `_`.

The return type of `Match` in this example is inferred to be `string`.

Do note that it is not possible to return `void`; this is done on purpose to adhere to the functional principle of always returning a value. To learn more about the reasons behind this, look up the empty tuple, better known as the "unit" value. There exist libraries that implement such a type in C#, or you can write it yourself with a few lines of code.

#### Automatically filling in missing cases
Because it is tedious to fill out all the cases yourself I've written a code fix provider that will do this for you. If you call the `Match()` method but leave some (or all) of the arguments out, you should receive a light bulb icon in your IDE that will implement all the cases for you automatically.

Note that this only currently works for non-parameterized invocations, e.g. if you specify a concrete type parameter like `Match<string>()` the code fix provider is currently not able to handle this. So if you are for example coming back to add a missing case and the invocation is not inferring the type parameter but is explicitly stating it, you can temporarily remove it to activate this code fix. This is being tracked in [#27](https://github.com/nemuikoneko/DiscriminatedUnions/issues/27).

### Matching with default fallback
Sometimes exhaustive matching is not desirable, e.g. if you only care about checking a few of the cases but don't care about the rest.

The `MatchWithDefault` method lets you specify a default fallback which only gets called if none of the other cases matched:
```csharp
var parseError = ParseError.TooShort;
var readableErrorMessage = parseError.MatchWithDefault(
    _: () => "Parsing failed, not important why", // This lambda will be called since the other case does not match the active case
    TooLong: () => "Too long!");
```

It is strongly recommended to **not** use this as it takes away one of the biggest advantages of using a discriminated union in the first place, namely exhaustive matching. Only use this if you know what you're doing.

### Union nested inside another type
Unions can be nested inside other types (note that the parent must also be `partial`):
```csharp
partial class/struct/record ParentType
{
    [DiscriminatedUnion]
    partial struct ChildUnion
    {
        interface Cases
        {
            void MyCase();
        }
    }
}
```

### Enhancing the type
Because the union is generated from a `partial` type, this means that the type itself is still just a C# type, and can be used as one would normally implement custom types (although it is recommended to keep union types as simple as possible).

For example one could easily add implicit cast operators to make the resulting code clean and compact:
```csharp
[DiscriminatedUnion]
public readonly partial struct Result<TOk, TErr>
{
    interface Cases
    {
        void Ok(TOk obj);
        void Err(TErr obj);
    }

    public static implicit operator Result<TOk, TErr>(TOk obj) => Ok(obj);
    public static implicit operator Result<TOk, TErr>(TErr obj) => Err(obj);
}

public sealed partial class Username
{
    private Username(string value) => Value = value;

    public string Value { get; }

    public static Result<Username, ParseError> Parse(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return ParseError.TooShort;

        if (username.Length >= 20)
            return ParseError.TooLong;

        if (username.Contains('@'))
            return ParseError.IllegalSymbol('@');

        return new Username(username);
    }

    [DiscriminatedUnion]
    public readonly partial struct ParseError
    {
        interface Cases
        {
            void TooShort();
            void TooLong();
            void IllegalSymbol(char illegalSymbol);
        }
    }
}

var result = Username.Parse("Bob");

var usernameOrParseErrorAsString = result.Match(
    Ok: username => username.Value,
    Err: err => err.Match(
        TooShort: () => "Username was too short",
        TooLong: () => "Username was too long",
        IllegalSymbol: _ => "Username contained illegal symbols"));
```

By using nested types this allows us to avoid naming such as `UsernameParseError`, and by using implicit casts we don't need to return with something like `return Result<Username, ParseError>.Err(ParseError.TooShort)`.

Do note that when we begin to match on the outcome the code may become a bit bloated since we are required to unwrap each inner match expression all the way. This boilerplate can be reduced by taking common usage of the match and turn it into a method or implicit cast, e.g. `ParseError.ToHumanReadableString()`.

An alternative is to use a more functional approach by composing function calls using the monad pattern, which would significantly reduce the boilerplate and ensure type safety all the way through.

**Important:** You should let the union be generated without any additional code first, then add your custom code to the type to avoid any problems with the generation.

### Union with default initialization
Some unions need to be defaultable, e.g. `Option<T>` is a good candidate since one may want to use it as a replacement for `arg? = null` in a method argument list.

Default initialization can be permitted by using the `AllowDefault` property on the `DiscriminatedUnion` attribute:
```csharp
[DiscriminatedUnion(AllowDefault=true)]
partial struct UnionWithDefault
{
    interface Cases
    {
        void MyCase();
    }
}
```

By enabling this the scenarios below will be allowed to compile:
```csharp
static void SomeMethod(UnionWithDefault x = default) { }

var a = default(UnionWithDefault);
UnionWithDefault b = default;

var c = new UnionWithDefault();
UnionWithDefault d = new();
```

For unions allowing default initialization there are some requirements that ensures there is something to default to:
- The union must have at least one case
- The first case must take no arguments

Failing to fulfill these criteria will not generate any warnings and instead just not generate the union.

It is not recommended to allow a union to be defaultable unless you have a good reason to.

### Union as a recursive type
One nice property of discriminated unions is they lend themselves nicely to type modeling. In the example below we build a JSON structure programmatically, then convert it to a displayable string (rewritten example from [this](http://book.realworldhaskell.org/read/writing-a-library-working-with-json-data.html) book):
```csharp
[DiscriminatedUnion]
public readonly partial struct JValue
{
    interface Cases
    {
        void JString(string s);
        void JNumber(double d);
        void JBool(bool b);
        void JNull();
        void JObject((string, JValue)[] obj);
        void JArray(JValue[] arr);
    }
}

public static string RenderJValue(JValue jValue)
{
    static string RenderValues(JValue[] arr)
        => arr.Length == 0
            ? string.Empty
            : string.Join(", ", arr.Select(RenderJValue));

    static string RenderPair((string, JValue) pair)
        => $"{pair.Item1}: {RenderJValue(pair.Item2)}";

    static string RenderPairs((string, JValue)[] obj)
        => obj.Length == 0
            ? string.Empty
            : string.Join(", ", obj.Select(RenderPair));

    return jValue.Match(
        JString: s => $"\"{s}\"",
        JNumber: num => num.ToString(),
        JNull: () => "null",
        JBool: b => b == true ? "true" : "false",
        JArray: arr => $"[{RenderValues(arr)}]",
        JObject: obj => $"{{{RenderPairs(obj)}}}");
}
    
var jValue = JValue.JObject(new[]
{
    ("name", JValue.JString("Robert")),
    ("age", JValue.JNumber(45)),
    ("nicknames", JValue.JArray(new[]
    {
        JValue.JString("Bob"),
        JValue.JString("Bobby"),
        JValue.JString("Rob")
    }))
});

var json = RenderJValue(jValue); // {name: "Robert", age: 45, nicknames: ["Bob", "Bobby", "Rob"]}
```

## Release Notes
### 1.0.0-beta
Basic functionality. Not yet stable.
