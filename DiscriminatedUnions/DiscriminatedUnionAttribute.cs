using System;

namespace DiscriminatedUnions;

public class DiscriminatedUnionAttribute : Attribute
{
    public bool AllowDefault { get; set; }
}
