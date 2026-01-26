using Shouldly;
using Lopen.Core.Testing;

namespace Lopen.Core.Tests.Testing;

public class KeywordValidatorTests
{
    [Fact]
    public void Validate_WithMatchingKeyword_ReturnsValid()
    {
        var validator = new KeywordValidator("hello", "world");
        
        var result = validator.Validate("Hello, how are you?");
        
        result.IsValid.ShouldBeTrue();
        result.MatchedPattern.ShouldBe("hello");
    }
    
    [Fact]
    public void Validate_WithNoMatchingKeyword_ReturnsInvalid()
    {
        var validator = new KeywordValidator("foo", "bar");
        
        var result = validator.Validate("Hello, how are you?");
        
        result.IsValid.ShouldBeFalse();
        result.MatchedPattern.ShouldBeNull();
    }
    
    [Fact]
    public void Validate_IsCaseInsensitive()
    {
        var validator = new KeywordValidator("HELLO");
        
        var result = validator.Validate("hello world");
        
        result.IsValid.ShouldBeTrue();
        result.MatchedPattern.ShouldBe("HELLO");
    }
    
    [Fact]
    public void Validate_WithEmptyResponse_ReturnsInvalid()
    {
        var validator = new KeywordValidator("hello");
        
        var result = validator.Validate("");
        
        result.IsValid.ShouldBeFalse();
    }
    
    [Fact]
    public void Validate_WithNullResponse_ReturnsInvalid()
    {
        var validator = new KeywordValidator("hello");
        
        var result = validator.Validate(null!);
        
        result.IsValid.ShouldBeFalse();
    }
    
    [Fact]
    public void Validate_WithEmptyKeywords_ReturnsInvalid()
    {
        var validator = new KeywordValidator(Array.Empty<string>());
        
        var result = validator.Validate("hello world");
        
        result.IsValid.ShouldBeFalse();
    }
    
    [Fact]
    public void Validate_AnyMode_ReturnsFirstMatch()
    {
        var validator = new KeywordValidator(new[] { "first", "second" }, MatchMode.Any);
        
        var result = validator.Validate("second comes before first sometimes");
        
        result.IsValid.ShouldBeTrue();
        result.MatchedPattern.ShouldBe("first");
    }
    
    [Fact]
    public void Validate_AllMode_RequiresAllKeywords()
    {
        var validator = new KeywordValidator(new[] { "hello", "world" }, MatchMode.All);
        
        var result = validator.Validate("hello world!");
        
        result.IsValid.ShouldBeTrue();
        result.MatchedPattern.ShouldNotBeNull();
        result.MatchedPattern.ShouldContain("hello");
        result.MatchedPattern.ShouldContain("world");
    }
    
    [Fact]
    public void Validate_AllMode_FailsIfMissingKeyword()
    {
        var validator = new KeywordValidator(new[] { "hello", "world" }, MatchMode.All);
        
        var result = validator.Validate("hello there!");
        
        result.IsValid.ShouldBeFalse();
    }
    
    [Fact]
    public void Validate_MatchesSubstring()
    {
        var validator = new KeywordValidator("ell");
        
        var result = validator.Validate("Hello");
        
        result.IsValid.ShouldBeTrue();
        result.MatchedPattern.ShouldBe("ell");
    }
}
