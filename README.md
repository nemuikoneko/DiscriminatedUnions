# Discriminated Unions
## Overview
This library contains a source generator for creating Discriminated Unions in C# (also known as Tagged Unions or Choice Types).

The generated unions are meant to be more robust than other techniques used to build discriminated unions, e.g. subclassing an abstract class or record. The problem with these is that they are open-ended and the compiler does not lend us enough help to perform exhaustive matching. Exhaustive matching exists for plain enums, but 1) exhaustiveness is a suggestion, not a requirement, and 2) different editors suggest exhaustiveness differently. This library ensures that all cases have been accounted for when matching against the union's case.

The generated union is also a struct, meaning that it cannot be null. This is further enforced by an analyzer that makes sure neither `new` or `default` is going to work and a union can only be instantiated by explicitly selecting a case.

Unions can be compared and will be equal when their cases and data match.

Note that the union cases can not be pattern matched against, which is a downside.

## Important note

TODO Source generator IDE reload bug

## Usage

A union will only be generated when these criteria are met:
- The type is a partial struct
- The type is marked with the `[DiscriminatedUnion]` attribute from the `nemuikoneko.DiscriminatedUnions` namespace
- The type contains an interface named `Cases` whose members are of the format `void CaseName(<arguments>);`
- There exists at least one union case

For unions allowing default initialization this is also required:
- The union must have at least one case
- The first case must take no arguments

Note that no error will be reported if these critera are not met; the union will simply not be generated.
The generated union will exist as a partial type in a generated file and can be inspected by pressing F12 in the IDE and looking at the implementation.

## Examples
### Basic union
```
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

### Generic union
```
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

### Union nested inside type
### Union with default initialization
### Matching
### Matching with default fallback
### Enhancing the type
Implicit operators etc.

## Release Notes
### 1.0.0-beta
Basic functionality. Not yet stable.
