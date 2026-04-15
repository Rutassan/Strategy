using System.Numerics;
using Raylib_cs;
using Strategy;
using Xunit;

namespace Strategy.Tests;

public sealed class WorldMapModelTests
{
    private readonly WorldMapModel _model = new();

    [Theory]
    [InlineData(-1.0f, 0)]
    [InlineData(-0.75f, 1)]
    [InlineData(-0.10f, 2)]
    [InlineData(0.94f, 2)]
    [InlineData(0.95f, 3)]
    public void Возвращает_ожидаемый_тип_тайла_по_высоте(float value, int expectedType)
    {
        int tileType = WorldMapModel.GetTileType(value);

        Assert.Equal(expectedType, tileType);
    }

    [Fact]
    public void Генерация_карты_создает_сетку_правильного_размера_и_диапазона()
    {
        int[,] tiles = WorldMapModel.GenerateTiles();

        Assert.Equal(WorldMapModel.MapHeightValue, tiles.GetLength(0));
        Assert.Equal(WorldMapModel.MapWidthValue, tiles.GetLength(1));

        foreach (int tile in tiles)
        {
            Assert.InRange(tile, 0, 3);
        }
    }

    [Fact]
    public void Экземпляр_модели_хранит_сгенерированные_тайлы()
    {
        Assert.Equal(WorldMapModel.MapHeightValue, _model.Tiles.GetLength(0));
        Assert.Equal(WorldMapModel.MapWidthValue, _model.Tiles.GetLength(1));
    }

    [Fact]
    public void Экземпляр_модели_хранит_карту_провинций_и_список_провинций()
    {
        Assert.Equal(WorldMapModel.MapHeightValue, _model.ProvinceMap.GetLength(0));
        Assert.Equal(WorldMapModel.MapWidthValue, _model.ProvinceMap.GetLength(1));
        Assert.Equal(96, _model.Provinces.Count);
        Assert.Equal(576, _model.Characters.Count);
    }

    [Fact]
    public void Начальная_камера_смотрит_в_центр_карты()
    {
        Vector2 center = _model.InitialCameraTarget;

        Assert.Equal(_model.MapWidthInPixels * 0.5f, center.X);
        Assert.Equal(_model.MapHeightInPixels * 0.5f, center.Y);
    }

    [Fact]
    public void Генерация_карты_провинций_помечает_все_тайлы()
    {
        int[,] provinceMap = WorldMapModel.GenerateProvinceMap();

        Assert.Equal(0, provinceMap[0, 0]);
        Assert.Equal(1, provinceMap[0, WorldMapModel.ProvinceWidthInTiles]);
        Assert.Equal(12, provinceMap[WorldMapModel.ProvinceHeightInTiles, 0]);
        Assert.Equal(95, provinceMap[WorldMapModel.MapHeightValue - 1, WorldMapModel.MapWidthValue - 1]);
    }

    [Fact]
    public void Генерация_провинций_создает_названия_и_доминирующий_тип_местности()
    {
        (IReadOnlyList<Province> provinces, IReadOnlyDictionary<int, Character> characters) = WorldMapModel.GenerateWorldData(_model.Tiles, _model.ProvinceMap);

        Assert.Equal(96, provinces.Count);
        Assert.StartsWith("Графство ", provinces[0].CountyName);
        Assert.StartsWith("Барония ", provinces[0].BaronyName);
        Assert.InRange(provinces[0].TerrainType, 0, 3);
        Assert.True(provinces[0].OwnerCharacterId > 0);
        Assert.True(characters.ContainsKey(provinces[0].OwnerCharacterId));
    }

    [Fact]
    public void Панорамирование_с_нулевым_направлением_не_двигает_камеру()
    {
        Vector2 target = new(10f, 20f);

        Vector2 result = _model.ApplyKeyboardPan(target, Vector2.Zero, 1f, 1f);

        Assert.Equal(target, result);
    }

    [Fact]
    public void Панорамирование_с_клавиатуры_нормализует_диагональ()
    {
        Vector2 result = _model.ApplyKeyboardPan(Vector2.Zero, new Vector2(1f, -1f), 0.5f, 2f);
        float expectedComponent = MathF.Sqrt(0.5f) * WorldMapModel.KeyboardPanSpeed * 0.5f / 2f;

        Assert.Equal(expectedComponent, result.X, 4);
        Assert.Equal(-expectedComponent, result.Y, 4);
    }

    [Fact]
    public void Панорамирование_мышью_сдвигает_камеру_обратно_движению_курсора()
    {
        Vector2 result = _model.ApplyMousePan(new Vector2(100f, 200f), new Vector2(20f, -10f), 2f);

        Assert.Equal(new Vector2(90f, 205f), result);
    }

    [Theory]
    [InlineData(1.0f, 1.0f, 1.12f)]
    [InlineData(1.0f, -10.0f, WorldMapModel.MinZoom)]
    [InlineData(2.4f, 1.0f, WorldMapModel.MaxZoom)]
    public void Зум_ограничивается_и_масштабируется_плавно(float currentZoom, float wheelDelta, float expectedZoom)
    {
        float result = _model.ApplyZoom(currentZoom, wheelDelta);

        Assert.Equal(expectedZoom, result, 4);
    }

    [Fact]
    public void Зум_вокруг_курсора_сохраняет_точку_под_мышью()
    {
        Vector2 result = _model.AdjustTargetForCursorZoom(new Vector2(50f, 80f), new Vector2(400f, 300f), new Vector2(390f, 280f));

        Assert.Equal(new Vector2(60f, 100f), result);
    }

    [Fact]
    public void Ограничение_камеры_зажимает_координаты_внутри_карты()
    {
        Vector2 result = _model.ClampCameraTarget(new Vector2(-500f, 999999f), 1600, 900, 1f);

        Assert.Equal(800f, result.X);
        Assert.Equal(_model.MapHeightInPixels - 450f, result.Y);
    }

    [Fact]
    public void Ограничение_камеры_центрирует_вид_если_экран_больше_карты()
    {
        Vector2 result = _model.ClampCameraTarget(new Vector2(0f, 0f), 20000, 20000, 1f);

        Assert.Equal(_model.MapWidthInPixels * 0.5f, result.X);
        Assert.Equal(_model.MapHeightInPixels * 0.5f, result.Y);
    }

    [Fact]
    public void Поиск_провинции_по_координате_мира_возвращает_нужную_область()
    {
        Province? province = _model.GetProvinceAtWorldPosition(new Vector2(10f, 10f));

        Assert.NotNull(province);
        Assert.Equal(0, province!.Id);
        Assert.True(province.ContainsTile(0, 0));
        Assert.True(province.OwnerCharacterId > 0);
    }

    [Fact]
    public void Поиск_провинции_вне_карты_возвращает_null()
    {
        Province? province = _model.GetProvinceAtWorldPosition(new Vector2(-5f, 100f));

        Assert.Null(province);
    }

    [Fact]
    public void Проверка_границ_карты_работает_для_внутренних_и_внешних_клеток()
    {
        Assert.True(_model.IsTileInside(0, 0));
        Assert.True(_model.IsTileInside(WorldMapModel.MapWidthValue - 1, WorldMapModel.MapHeightValue - 1));
        Assert.False(_model.IsTileInside(-1, 0));
        Assert.False(_model.IsTileInside(WorldMapModel.MapWidthValue, 0));
    }

    [Fact]
    public void Граница_провинции_определяется_между_соседними_областями_и_краем_карты()
    {
        Assert.False(_model.HasProvinceBorder(0, 0, 1, 0));
        Assert.True(_model.HasProvinceBorder(WorldMapModel.ProvinceWidthInTiles - 1, 0, 1, 0));
        Assert.True(_model.HasProvinceBorder(0, 0, -1, 0));
        Assert.False(_model.HasProvinceBorder(-1, 0, 1, 0));
    }

    [Theory]
    [InlineData(0, 1, 2, 42, 103, 159, 255)]
    [InlineData(1, 1, 2, 229, 207, 141, 255)]
    [InlineData(2, 1, 2, 85, 152, 85, 255)]
    [InlineData(3, 1, 2, 117, 123, 133, 255)]
    public void Цвет_тайла_зависит_от_типа_и_вариации(int tileType, int x, int y, int r, int g, int b, int a)
    {
        Color color = _model.GetTileColor(tileType, x, y);

        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
        Assert.Equal(a, color.A);
    }

    [Fact]
    public void Провинция_знает_свои_границы_и_центр()
    {
        var province = new Province(4, "Графство Тест", "Барония Тест", 8, 12, 4, 4, 2, 99);

        Assert.Equal(11, province.EndTileX);
        Assert.Equal(15, province.EndTileY);
        Assert.True(province.ContainsTile(9, 13));
        Assert.False(province.ContainsTile(12, 13));
        Assert.Equal(new Vector2(1280f, 1792f), province.GetCenterInPixels(WorldMapModel.TileSizeValue));
        Assert.Equal(99, province.OwnerCharacterId);
    }

    [Fact]
    public void Можно_получить_владельца_провинции_и_персонажа_по_id()
    {
        Province province = _model.Provinces[0];
        Character? owner = _model.GetProvinceOwner(province);

        Assert.NotNull(owner);
        Assert.Equal(province.OwnerCharacterId, owner!.Id);
        Assert.Equal(owner, _model.GetCharacterById(owner.Id));
    }

    [Fact]
    public void Сгенерированный_владелец_имеет_династию_семью_и_навыки()
    {
        Character owner = _model.Characters[_model.Provinces[0].OwnerCharacterId];

        Assert.StartsWith("Кня", owner.Title);
        Assert.False(string.IsNullOrWhiteSpace(owner.HouseName));
        Assert.Equal(8, owner.Traits.Count);
        Assert.NotNull(owner.SpouseId);
        Assert.Equal(2, owner.ParentIds.Count);
        Assert.Equal(2, owner.ChildIds.Count);
        Assert.InRange(owner.Skills.Diplomacy, 4, 22);
        Assert.True(owner.IsAlive);
    }

    [Fact]
    public void Персонаж_и_его_навыки_отдают_все_свойства()
    {
        Character owner = _model.Characters[_model.Provinces[0].OwnerCharacterId];

        Assert.False(string.IsNullOrWhiteSpace(owner.FullName));
        Assert.InRange(owner.Age, 18, 80);
        Assert.False(string.IsNullOrWhiteSpace(owner.Gender));
        Assert.True(owner.Gold > 0);
        Assert.True(owner.Prestige > 0);
        Assert.True(owner.Piety > 0);
        Assert.NotEqual(default, owner.BannerColor);

        Assert.InRange(owner.Skills.Martial, 4, 22);
        Assert.InRange(owner.Skills.Stewardship, 4, 22);
        Assert.InRange(owner.Skills.Intrigue, 4, 22);
    }

    [Fact]
    public void Можно_получить_null_для_несуществующего_персонажа()
    {
        Assert.Null(_model.GetCharacterById(-1));
    }

    [Fact]
    public void Можно_получить_стартовый_список_доступных_правителей()
    {
        IReadOnlyList<Character> rulers = _model.GetSelectableRulers(12);

        Assert.Equal(12, rulers.Count);
        Assert.All(rulers, ruler => Assert.Contains(_model.Provinces, province => province.OwnerCharacterId == ruler.Id));
    }

    [Fact]
    public void Можно_получить_провинции_принадлежащие_персонажу()
    {
        Character ruler = _model.GetSelectableRulers(1).Single();
        IReadOnlyList<Province> provinces = _model.GetOwnedProvinces(ruler.Id);

        Assert.Single(provinces);
        Assert.True(_model.IsProvinceOwnedByCharacter(provinces[0], ruler.Id));
        Assert.False(_model.IsProvinceOwnedByCharacter(provinces[0], ruler.Id + 1));
    }
}
