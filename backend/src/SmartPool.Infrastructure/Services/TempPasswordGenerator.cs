using System.Security.Cryptography;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Services;

public sealed class TempPasswordGenerator : ITempPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Upper + Lower + Digits;
    private const int Length = 10;

    public string Generate()
    {
        Span<char> characters = stackalloc char[Length];
        characters[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        characters[1] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        for (var index = 2; index < Length; index++)
            characters[index] = All[RandomNumberGenerator.GetInt32(All.Length)];
        RandomNumberGenerator.Shuffle(characters);
        return new string(characters);
    }
}
