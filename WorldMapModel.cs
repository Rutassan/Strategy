using System.Numerics;
using Raylib_cs;

namespace Strategy;

internal sealed class WorldMapModel
{
    public const int TileSizeValue = 128;
    public const int MapWidthValue = 48;
    public const int MapHeightValue = 32;
    public const float MinZoom = 0.35f;
    public const float MaxZoom = 2.5f;
    public const float DefaultZoom = 0.9f;
    public const float KeyboardPanSpeed = 1200f;
    public const float MousePanScale = 1f;
    public const int ProvinceWidthInTiles = 4;
    public const int ProvinceHeightInTiles = 4;
    public const int ProvinceCountX = MapWidthValue / ProvinceWidthInTiles;
    public const int ProvinceCountY = MapHeightValue / ProvinceHeightInTiles;

    public int[,] Tiles { get; }
    public int[,] ProvinceMap { get; }
    public IReadOnlyList<Province> Provinces { get; }
    public IReadOnlyDictionary<int, Character> Characters { get; }

    public int MapWidth => MapWidthValue;
    public int MapHeight => MapHeightValue;
    public int TileSize => TileSizeValue;
    public int MapWidthInPixels => MapWidth * TileSize;
    public int MapHeightInPixels => MapHeight * TileSize;
    public Vector2 InitialCameraTarget => new(MapWidthInPixels * 0.5f, MapHeightInPixels * 0.5f);

    public WorldMapModel()
    {
        Tiles = GenerateTiles();
        ProvinceMap = GenerateProvinceMap();
        (IReadOnlyList<Province> provinces, IReadOnlyDictionary<int, Character> characters) = GenerateWorldData(Tiles, ProvinceMap);
        Provinces = provinces;
        Characters = characters;
    }

    public static int[,] GenerateTiles()
    {
        var tiles = new int[MapHeightValue, MapWidthValue];

        for (int y = 0; y < MapHeightValue; y++)
        {
            for (int x = 0; x < MapWidthValue; x++)
            {
                float largeNoise = MathF.Sin(x * 0.24f) + MathF.Cos(y * 0.21f);
                float detailNoise = MathF.Sin((x + y) * 0.58f) * 0.35f + MathF.Cos((x - y) * 0.47f) * 0.22f;
                float value = largeNoise + detailNoise;

                tiles[y, x] = GetTileType(value);
            }
        }

        return tiles;
    }

    public static int GetTileType(float heightValue)
    {
        return heightValue switch
        {
            < -0.75f => 0,
            < -0.1f => 1,
            < 0.95f => 2,
            _ => 3
        };
    }

    public static int[,] GenerateProvinceMap()
    {
        var provinceMap = new int[MapHeightValue, MapWidthValue];
        int provinceId = 0;

        for (int startY = 0; startY < MapHeightValue; startY += ProvinceHeightInTiles)
        {
            for (int startX = 0; startX < MapWidthValue; startX += ProvinceWidthInTiles)
            {
                int width = Math.Min(ProvinceWidthInTiles, MapWidthValue - startX);
                int height = Math.Min(ProvinceHeightInTiles, MapHeightValue - startY);

                for (int y = startY; y < startY + height; y++)
                {
                    for (int x = startX; x < startX + width; x++)
                    {
                        provinceMap[y, x] = provinceId;
                    }
                }

                provinceId++;
            }
        }

        return provinceMap;
    }

    public static (IReadOnlyList<Province> Provinces, IReadOnlyDictionary<int, Character> Characters) GenerateWorldData(int[,] tiles, int[,] provinceMap)
    {
        var provinces = new List<Province>();
        var characters = new Dictionary<int, Character>();
        int nextCharacterId = 1;

        for (int provinceY = 0; provinceY < ProvinceCountY; provinceY++)
        {
            for (int provinceX = 0; provinceX < ProvinceCountX; provinceX++)
            {
                int provinceId = provinceY * ProvinceCountX + provinceX;
                int startTileX = provinceX * ProvinceWidthInTiles;
                int startTileY = provinceY * ProvinceHeightInTiles;
                int terrainType = GetDominantTerrain(tiles, startTileX, startTileY, ProvinceWidthInTiles, ProvinceHeightInTiles);
                int ownerId = nextCharacterId;
                string houseName = BuildHouseName(provinceX, provinceY, terrainType);

                foreach (Character character in BuildProvinceCharacters(ownerId, provinceX, provinceY, terrainType, houseName))
                {
                    characters[character.Id] = character;
                }

                nextCharacterId += 6;

                provinces.Add(new Province(
                    provinceId,
                    BuildCountyName(provinceX, provinceY, terrainType),
                    BuildBaronyName(provinceX, provinceY, terrainType),
                    startTileX,
                    startTileY,
                    ProvinceWidthInTiles,
                    ProvinceHeightInTiles,
                    terrainType,
                    ownerId));
            }
        }

        return (provinces, characters);
    }

    public Vector2 ApplyKeyboardPan(Vector2 currentTarget, Vector2 direction, float deltaTime, float zoom)
    {
        if (direction == Vector2.Zero)
        {
            return currentTarget;
        }

        Vector2 normalized = Vector2.Normalize(direction);
        return currentTarget + normalized * KeyboardPanSpeed * deltaTime / zoom;
    }

    public Vector2 ApplyMousePan(Vector2 currentTarget, Vector2 mouseDelta, float zoom)
    {
        return currentTarget - mouseDelta / zoom * MousePanScale;
    }

    public float ApplyZoom(float currentZoom, float wheelDelta)
    {
        return Math.Clamp(currentZoom + wheelDelta * 0.12f * currentZoom, MinZoom, MaxZoom);
    }

    public Vector2 AdjustTargetForCursorZoom(Vector2 currentTarget, Vector2 mouseWorldBeforeZoom, Vector2 mouseWorldAfterZoom)
    {
        return currentTarget + (mouseWorldBeforeZoom - mouseWorldAfterZoom);
    }

    public Vector2 ClampCameraTarget(Vector2 target, int screenWidth, int screenHeight, float zoom)
    {
        float viewportHalfWidth = screenWidth * 0.5f / zoom;
        float viewportHalfHeight = screenHeight * 0.5f / zoom;

        float minTargetX = viewportHalfWidth;
        float maxTargetX = MapWidthInPixels - viewportHalfWidth;
        float minTargetY = viewportHalfHeight;
        float maxTargetY = MapHeightInPixels - viewportHalfHeight;

        if (minTargetX > maxTargetX)
        {
            minTargetX = maxTargetX = MapWidthInPixels * 0.5f;
        }

        if (minTargetY > maxTargetY)
        {
            minTargetY = maxTargetY = MapHeightInPixels * 0.5f;
        }

        return new Vector2(
            Math.Clamp(target.X, minTargetX, maxTargetX),
            Math.Clamp(target.Y, minTargetY, maxTargetY));
    }

    public Color GetTileColor(int tileType, int x, int y)
    {
        int variation = (x * 17 + y * 31) % 18;

        return tileType switch
        {
            0 => new Color(35 + variation, 96 + variation, 152 + variation, 255),
            1 => new Color(222 + variation, 204 + variation / 2, 139 + variation / 3, 255),
            2 => new Color(78 + variation, 145 + variation, 82 + variation / 2, 255),
            _ => new Color(110 + variation, 116 + variation, 126 + variation, 255)
        };
    }

    public Province? GetProvinceAtWorldPosition(Vector2 worldPosition)
    {
        int tileX = (int)MathF.Floor(worldPosition.X / TileSize);
        int tileY = (int)MathF.Floor(worldPosition.Y / TileSize);

        if (!IsTileInside(tileX, tileY))
        {
            return null;
        }

        int provinceId = ProvinceMap[tileY, tileX];
        return Provinces[provinceId];
    }

    public Character? GetCharacterById(int characterId)
    {
        return Characters.GetValueOrDefault(characterId);
    }

    public Character? GetProvinceOwner(Province province)
    {
        return GetCharacterById(province.OwnerCharacterId);
    }

    public IReadOnlyList<Character> GetSelectableRulers(int maxCount)
    {
        return Provinces
            .Select(GetProvinceOwner)
            .OfType<Character>()
            .OrderBy(character => character.HouseName)
            .ThenBy(character => character.FullName)
            .Take(maxCount)
            .ToArray();
    }

    public IReadOnlyList<Province> GetOwnedProvinces(int characterId)
    {
        return Provinces
            .Where(province => province.OwnerCharacterId == characterId)
            .ToArray();
    }

    public bool IsProvinceOwnedByCharacter(Province province, int characterId)
    {
        return province.OwnerCharacterId == characterId;
    }

    public bool IsTileInside(int tileX, int tileY)
    {
        return tileX >= 0 && tileX < MapWidth && tileY >= 0 && tileY < MapHeight;
    }

    public bool HasProvinceBorder(int tileX, int tileY, int offsetX, int offsetY)
    {
        int neighborX = tileX + offsetX;
        int neighborY = tileY + offsetY;

        if (!IsTileInside(tileX, tileY))
        {
            return false;
        }

        if (!IsTileInside(neighborX, neighborY))
        {
            return true;
        }

        return ProvinceMap[tileY, tileX] != ProvinceMap[neighborY, neighborX];
    }

    private static int GetDominantTerrain(int[,] tiles, int startX, int startY, int width, int height)
    {
        var counts = new int[4];

        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                counts[tiles[y, x]]++;
            }
        }

        int dominantTerrain = 0;
        int dominantCount = counts[0];

        for (int terrainType = 1; terrainType < counts.Length; terrainType++)
        {
            if (counts[terrainType] > dominantCount)
            {
                dominantTerrain = terrainType;
                dominantCount = counts[terrainType];
            }
        }

        return dominantTerrain;
    }

    private static IReadOnlyList<Character> BuildProvinceCharacters(int ownerId, int provinceX, int provinceY, int terrainType, string houseName)
    {
        bool maleOwner = (provinceX + provinceY) % 2 == 0;
        int spouseId = ownerId + 1;
        int fatherId = ownerId + 2;
        int motherId = ownerId + 3;
        int firstChildId = ownerId + 4;
        int secondChildId = ownerId + 5;

        Color bannerColor = BuildHouseColor(ownerId);
        Color portraitColor = BuildPortraitColor(ownerId);
        string title = maleOwner ? "Князь" : "Княгиня";
        string firstName = GetFirstName(ownerId, maleOwner);
        string surname = BuildDynastySurname(houseName, maleOwner);
        string ownerName = $"{title} {firstName} {surname}";
        int ownerAge = 28 + (ownerId * 7 + terrainType * 3) % 31;

        var owner = new Character(
            ownerId,
            ownerName,
            houseName,
            title,
            ownerAge,
            maleOwner ? "Мужчина" : "Женщина",
            true,
            BuildSkills(ownerId),
            BuildTraits(ownerId, terrainType),
            spouseId,
            new[] { fatherId, motherId },
            new[] { firstChildId, secondChildId },
            120 + ownerId * 3,
            60 + ownerId * 4,
            35 + ownerId * 2,
            bannerColor,
            portraitColor);

        bool spouseMale = !maleOwner;
        string spouseTitle = spouseMale ? "Князь-супруг" : "Княгиня-супруга";
        var spouse = new Character(
            spouseId,
            $"{spouseTitle} {GetFirstName(spouseId, spouseMale)} {BuildDynastySurname(houseName, spouseMale)}",
            houseName,
            spouseTitle,
            Math.Max(18, ownerAge - 2 + ownerId % 5),
            spouseMale ? "Мужчина" : "Женщина",
            true,
            BuildSkills(spouseId),
            BuildTraits(spouseId, terrainType),
            ownerId,
            Array.Empty<int>(),
            new[] { firstChildId, secondChildId },
            80 + spouseId * 2,
            40 + spouseId * 3,
            22 + spouseId,
            bannerColor,
            BuildPortraitColor(spouseId));

        var father = new Character(
            fatherId,
            $"Князь {GetFirstName(fatherId, true)} {BuildDynastySurname(houseName, true)}",
            houseName,
            "Отец",
            ownerAge + 24,
            "Мужчина",
            ownerId % 3 != 0,
            BuildSkills(fatherId),
            BuildTraits(fatherId, terrainType),
            motherId,
            Array.Empty<int>(),
            new[] { ownerId },
            100 + fatherId,
            90 + fatherId * 2,
            55 + fatherId,
            bannerColor,
            BuildPortraitColor(fatherId));

        var mother = new Character(
            motherId,
            $"Княгиня {GetFirstName(motherId, false)} {BuildDynastySurname(houseName, false)}",
            houseName,
            "Мать",
            ownerAge + 21,
            "Женщина",
            true,
            BuildSkills(motherId),
            BuildTraits(motherId, terrainType),
            fatherId,
            Array.Empty<int>(),
            new[] { ownerId },
            95 + motherId,
            88 + motherId * 2,
            61 + motherId,
            bannerColor,
            BuildPortraitColor(motherId));

        var firstChild = new Character(
            firstChildId,
            $"Наследник {GetFirstName(firstChildId, true)} {BuildDynastySurname(houseName, true)}",
            houseName,
            "Сын",
            8 + ownerId % 9,
            "Мужчина",
            true,
            BuildSkills(firstChildId),
            BuildTraits(firstChildId, terrainType),
            null,
            new[] { ownerId, spouseId },
            Array.Empty<int>(),
            10 + firstChildId,
            4 + firstChildId,
            2 + firstChildId,
            bannerColor,
            BuildPortraitColor(firstChildId));

        var secondChild = new Character(
            secondChildId,
            $"Наследница {GetFirstName(secondChildId, false)} {BuildDynastySurname(houseName, false)}",
            houseName,
            "Дочь",
            6 + spouseId % 8,
            "Женщина",
            true,
            BuildSkills(secondChildId),
            BuildTraits(secondChildId, terrainType),
            null,
            new[] { ownerId, spouseId },
            Array.Empty<int>(),
            9 + secondChildId,
            3 + secondChildId,
            1 + secondChildId,
            bannerColor,
            BuildPortraitColor(secondChildId));

        return new[] { owner, spouse, father, mother, firstChild, secondChild };
    }

    private static string BuildCountyName(int provinceX, int provinceY, int terrainType)
    {
        string prefix = terrainType switch
        {
            0 => "Приозерное",
            1 => "Песчаное",
            2 => "Зеленое",
            _ => "Каменное"
        };

        return $"Графство {prefix} {provinceY + 1}-{provinceX + 1}";
    }

    private static string BuildHouseName(int provinceX, int provinceY, int terrainType)
    {
        string prefix = terrainType switch
        {
            0 => "Приозеры",
            1 => "Солегоры",
            2 => "Лесогоры",
            _ => "Скалогоры"
        };

        return $"{prefix} {provinceY + 1}-{provinceX + 1}";
    }

    private static string BuildBaronyName(int provinceX, int provinceY, int terrainType)
    {
        string prefix = terrainType switch
        {
            0 => "Волноград",
            1 => "Солегор",
            2 => "Лесной Рубеж",
            _ => "Скалистый Дозор"
        };

        return $"Барония {prefix} {provinceY + 1}-{provinceX + 1}";
    }

    private static string BuildDynastySurname(string houseName, bool male)
    {
        string stem = houseName.Split(' ')[0];
        return male ? $"{stem}ский" : $"{stem}ская";
    }

    private static CharacterSkills BuildSkills(int seed)
    {
        return new CharacterSkills(
            4 + seed * 3 % 19,
            4 + seed * 5 % 19,
            4 + seed * 7 % 19,
            4 + seed * 11 % 19,
            4 + seed * 13 % 19);
    }

    private static IReadOnlyList<string> BuildTraits(int seed, int terrainType)
    {
        string[][] traitPools =
        {
            new[] { "Решительный", "Мореплаватель", "Сдержанный", "Наблюдательный", "Упрямый", "Любит семью", "Осторожный", "Бережливый" },
            new[] { "Амбициозный", "Жадный", "Хитрый", "Трус", "Любит семью", "Терпеливый", "Расчетливый", "Выносливый" },
            new[] { "Справедливый", "Трудолюбивый", "Терпимый", "Амбициозный", "Добродушный", "Любит семью", "Охотник", "Настойчивый" },
            new[] { "Суровый", "Гордый", "Храбрый", "Подозрительный", "Амбициозный", "Верный", "Молчаливый", "Стойкий" }
        };

        string[] baseTraits = traitPools[terrainType];
        return Enumerable.Range(0, baseTraits.Length)
            .Select(index => baseTraits[(index + seed) % baseTraits.Length])
            .Distinct()
            .Take(8)
            .ToArray();
    }

    private static string GetFirstName(int seed, bool male)
    {
        string[] maleNames = { "Артур", "Борис", "Глеб", "Даниил", "Егор", "Игорь", "Лев", "Марк", "Олег", "Роман", "Святослав", "Тимур" };
        string[] femaleNames = { "Анна", "Вера", "Дарья", "Елена", "Ирина", "Лада", "Мария", "Нина", "Ольга", "Софья", "Таисия", "Юлия" };
        string[] names = male ? maleNames : femaleNames;
        return names[seed % names.Length];
    }

    private static Color BuildHouseColor(int seed)
    {
        return new Color(
            70 + seed * 37 % 140,
            60 + seed * 53 % 140,
            70 + seed * 71 % 140,
            255);
    }

    private static Color BuildPortraitColor(int seed)
    {
        return new Color(
            80 + seed * 19 % 120,
            90 + seed * 29 % 110,
            110 + seed * 31 % 100,
            255);
    }
}
