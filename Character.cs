using Raylib_cs;

namespace Strategy;

internal sealed record CharacterSkills(
    int Diplomacy,
    int Martial,
    int Stewardship,
    int Intrigue,
    int Learning);

internal sealed record Character(
    int Id,
    string FullName,
    string HouseName,
    string Title,
    int Age,
    string Gender,
    bool IsAlive,
    CharacterSkills Skills,
    IReadOnlyList<string> Traits,
    int? SpouseId,
    IReadOnlyList<int> ParentIds,
    IReadOnlyList<int> ChildIds,
    int Gold,
    int Prestige,
    int Piety,
    Color BannerColor,
    Color PortraitColor);
