using System.Diagnostics.CodeAnalysis;
using Raylib_cs;
using System.Numerics;

namespace Strategy;

[ExcludeFromCodeCoverage]
internal static class GameBootstrap
{
    private const string UiFontPath = "/usr/share/fonts/noto/NotoSans-Regular.ttf";

    public static void Run()
    {
        const int screenWidth = 1920;
        const int screenHeight = 1080;

        Raylib.SetConfigFlags(ConfigFlags.FullscreenMode | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(screenWidth, screenHeight, "AI Strategy");
        Raylib.SetTargetFPS(144);
        Raylib.SetExitKey(KeyboardKey.Null);

        int[] codepoints = BuildUiCodepoints();
        Font uiFont = Raylib.LoadFontEx(UiFontPath, 32, codepoints, codepoints.Length);
        var scene = new WorldMapScene(uiFont);

        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.F11))
            {
                Raylib.ToggleFullscreen();
            }

            float deltaTime = Raylib.GetFrameTime();
            scene.Update(deltaTime);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(187, 214, 234, 255));
            scene.Draw();
            Raylib.EndDrawing();
        }

        Raylib.UnloadFont(uiFont);
        Raylib.CloseWindow();
    }

    private static int[] BuildUiCodepoints()
    {
        const string glyphs =
            " АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ" +
            "абвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            "0123456789:./-x•()+,";

        return glyphs
            .Distinct()
            .Select(ch => (int)ch)
            .ToArray();
    }
}

[ExcludeFromCodeCoverage]
internal sealed class WorldMapScene
{
    private readonly WorldMapModel _model = new();
    private readonly Font _uiFont;
    private readonly IReadOnlyList<Character> _selectableRulers;
    private Camera2D _camera;
    private Province? _selectedProvince;
    private Character? _selectedCharacter;
    private Character? _playerCharacter;
    private bool _isRulerSelectionOpen;

    public WorldMapScene(Font uiFont)
    {
        _uiFont = uiFont;
        _selectableRulers = _model.GetSelectableRulers(12);
        _camera = new Camera2D
        {
            Target = _model.InitialCameraTarget,
            Offset = new Vector2(Raylib.GetScreenWidth() * 0.5f, Raylib.GetScreenHeight() * 0.5f),
            Rotation = 0f,
            Zoom = WorldMapModel.DefaultZoom
        };

        _camera.Target = _model.ClampCameraTarget(_camera.Target, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), _camera.Zoom);
    }

    public void Update(float deltaTime)
    {
        _camera.Offset = new Vector2(Raylib.GetScreenWidth() * 0.5f, Raylib.GetScreenHeight() * 0.5f);

        var keyboardDirection = new Vector2(
            GetAxis(KeyboardKey.A, KeyboardKey.Left, KeyboardKey.D, KeyboardKey.Right),
            GetAxis(KeyboardKey.W, KeyboardKey.Up, KeyboardKey.S, KeyboardKey.Down));

        _camera.Target = _model.ApplyKeyboardPan(_camera.Target, keyboardDirection, deltaTime, _camera.Zoom);

        if (Raylib.IsMouseButtonDown(MouseButton.Middle))
        {
            _camera.Target = _model.ApplyMousePan(_camera.Target, Raylib.GetMouseDelta(), _camera.Zoom);
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (MathF.Abs(wheel) >= float.Epsilon)
        {
            Vector2 mousePosition = Raylib.GetMousePosition();
            Vector2 mouseWorldBeforeZoom = Raylib.GetScreenToWorld2D(mousePosition, _camera);
            float zoom = _model.ApplyZoom(_camera.Zoom, wheel);
            var cameraAfterZoom = _camera;
            cameraAfterZoom.Zoom = zoom;
            Vector2 mouseWorldAfterZoom = Raylib.GetScreenToWorld2D(mousePosition, cameraAfterZoom);

            _camera.Zoom = zoom;
            _camera.Target = _model.AdjustTargetForCursorZoom(_camera.Target, mouseWorldBeforeZoom, mouseWorldAfterZoom);
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            HandleLeftClick(Raylib.GetMousePosition());
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _selectedCharacter = null;
        }

        _camera.Target = _model.ClampCameraTarget(_camera.Target, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), _camera.Zoom);
    }

    public void Draw()
    {
        Raylib.BeginMode2D(_camera);
        DrawTiles();
        DrawPlayerOwnedProvinces();
        DrawProvinceSelection();
        DrawProvinceBorders();
        DrawMapFrame();
        Raylib.EndMode2D();
        DrawHud();
        DrawChooseRulerButton();
        DrawPlayerPanel();
        DrawProvincePanel();
        DrawRulerSelectionPanel();
        DrawCharacterPanel();
    }

    private void DrawTiles()
    {
        for (int y = 0; y < _model.MapHeight; y++)
        {
            for (int x = 0; x < _model.MapWidth; x++)
            {
                var tileRect = new Rectangle(x * _model.TileSize, y * _model.TileSize, _model.TileSize, _model.TileSize);
                Raylib.DrawRectangleRec(tileRect, _model.GetTileColor(_model.Tiles[y, x], x, y));
            }
        }
    }

    private void DrawProvinceBorders()
    {
        Color borderColor = new Color(243, 238, 214, 165);

        for (int y = 0; y < _model.MapHeight; y++)
        {
            for (int x = 0; x < _model.MapWidth; x++)
            {
                int worldX = x * _model.TileSize;
                int worldY = y * _model.TileSize;
                int tileSize = _model.TileSize;

                if (_model.HasProvinceBorder(x, y, 0, -1))
                {
                    Raylib.DrawRectangle(worldX, worldY, tileSize, 4, borderColor);
                }

                if (_model.HasProvinceBorder(x, y, 0, 1))
                {
                    Raylib.DrawRectangle(worldX, worldY + tileSize - 4, tileSize, 4, borderColor);
                }

                if (_model.HasProvinceBorder(x, y, -1, 0))
                {
                    Raylib.DrawRectangle(worldX, worldY, 4, tileSize, borderColor);
                }

                if (_model.HasProvinceBorder(x, y, 1, 0))
                {
                    Raylib.DrawRectangle(worldX + tileSize - 4, worldY, 4, tileSize, borderColor);
                }
            }
        }
    }

    private void DrawMapFrame()
    {
        Raylib.DrawRectangleLinesEx(
            new Rectangle(0, 0, _model.MapWidthInPixels, _model.MapHeightInPixels),
            8f,
            new Color(16, 40, 52, 255));
    }

    private void DrawHud()
    {
        Raylib.DrawRectangle(18, 18, 330, 104, new Color(10, 22, 30, 205));
        Raylib.DrawTextEx(_uiFont, "AIS", new Vector2(32, 30), 28, 1, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, "ЛКМ: выбрать провинцию", new Vector2(32, 66), 20, 1, new Color(213, 231, 241, 255));
        Raylib.DrawTextEx(_uiFont, $"Камера: WASD СКМ колесо  {_camera.Zoom:0.00}x", new Vector2(32, 90), 20, 1, new Color(213, 231, 241, 255));
    }

    private void DrawChooseRulerButton()
    {
        Rectangle buttonRect = GetChooseRulerButtonRect();
        Color fillColor = _isRulerSelectionOpen ? new Color(90, 95, 62, 255) : new Color(70, 88, 106, 255);
        Raylib.DrawRectangleRounded(buttonRect, 0.18f, 8, fillColor);
        Raylib.DrawRectangleLinesEx(buttonRect, 2f, new Color(231, 220, 184, 255));
        string label = _playerCharacter is null ? "Начать правление" : "Выбрать персонажа";
        Raylib.DrawTextEx(_uiFont, label, new Vector2(buttonRect.X + 18, buttonRect.Y + 10), 22, 1, Color.RayWhite);
    }

    private void DrawPlayerPanel()
    {
        if (_playerCharacter is null)
        {
            return;
        }

        int panelX = 18;
        int panelY = 186;
        int panelWidth = 340;
        int panelHeight = 102;

        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(17, 24, 31, 228));
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelWidth, panelHeight), 3f, _playerCharacter.BannerColor);
        Raylib.DrawRectangle(panelX + 20, panelY + 18, 56, 56, _playerCharacter.PortraitColor);
        Raylib.DrawRectangleLines(panelX + 20, panelY + 18, 56, 56, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, "Вы", new Vector2(panelX + 92, panelY + 14), 24, 1, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, _playerCharacter.FullName, new Vector2(panelX + 92, panelY + 40), 18, 1, new Color(255, 235, 153, 255));
        Raylib.DrawTextEx(_uiFont, _playerCharacter.HouseName, new Vector2(panelX + 92, panelY + 66), 18, 1, new Color(217, 233, 241, 255));
    }

    private void DrawPlayerOwnedProvinces()
    {
        if (_playerCharacter is null)
        {
            return;
        }

        Color ownedFillColor = new Color((int)_playerCharacter.BannerColor.R, (int)_playerCharacter.BannerColor.G, (int)_playerCharacter.BannerColor.B, 92);
        Color ownedBorderColor = new Color(255, 245, 170, 255);

        foreach (Province province in _model.GetOwnedProvinces(_playerCharacter.Id))
        {
            for (int y = province.StartTileY; y <= province.EndTileY; y++)
            {
                for (int x = province.StartTileX; x <= province.EndTileX; x++)
                {
                    Raylib.DrawRectangle(x * _model.TileSize, y * _model.TileSize, _model.TileSize, _model.TileSize, ownedFillColor);
                }
            }

            Raylib.DrawRectangleLinesEx(
                new Rectangle(
                    province.StartTileX * _model.TileSize,
                    province.StartTileY * _model.TileSize,
                    province.WidthInTiles * _model.TileSize,
                    province.HeightInTiles * _model.TileSize),
                8f,
                ownedBorderColor);
        }
    }

    private void DrawProvinceSelection()
    {
        if (_selectedProvince is null)
        {
            return;
        }

        Color highlightColor = new Color(255, 240, 125, 48);
        Color centerColor = new Color(255, 240, 125, 230);

        for (int y = _selectedProvince.StartTileY; y <= _selectedProvince.EndTileY; y++)
        {
            for (int x = _selectedProvince.StartTileX; x <= _selectedProvince.EndTileX; x++)
            {
                Raylib.DrawRectangle(x * _model.TileSize, y * _model.TileSize, _model.TileSize, _model.TileSize, highlightColor);
            }
        }

        Vector2 center = _selectedProvince.GetCenterInPixels(_model.TileSize);
        Raylib.DrawCircleV(center, 12f, centerColor);
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, 18f, new Color(255, 255, 255, 220));
    }

    private void DrawProvincePanel()
    {
        if (_selectedProvince is null)
        {
            return;
        }

        const int panelWidth = 420;
        const int panelHeight = 212;
        int panelX = Raylib.GetScreenWidth() - panelWidth - 22;
        int panelY = 22;
        Character? owner = _model.GetProvinceOwner(_selectedProvince);

        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(17, 24, 31, 228));
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelWidth, panelHeight), 3f, new Color(236, 224, 186, 255));

        string terrainLabel = _selectedProvince.TerrainType switch
        {
            0 => "Водоемы и берега",
            1 => "Пески и степь",
            2 => "Леса и поля",
            _ => "Холмы и скалы"
        };

        Raylib.DrawTextEx(_uiFont, "Выбранная провинция", new Vector2(panelX + 22, panelY + 18), 27, 1, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, _selectedProvince.CountyName, new Vector2(panelX + 22, panelY + 58), 21, 1, new Color(235, 217, 158, 255));
        Raylib.DrawTextEx(_uiFont, _selectedProvince.BaronyName, new Vector2(panelX + 22, panelY + 87), 20, 1, new Color(217, 233, 241, 255));
        DrawProvinceOwnerLine(panelX + 22, panelY + 118, owner);
        Raylib.DrawTextEx(_uiFont, $"Династия: {owner?.HouseName ?? "Неизвестно"}", new Vector2(panelX + 22, panelY + 145), 18, 1, new Color(217, 233, 241, 255));
        Raylib.DrawTextEx(_uiFont, $"Территория: {terrainLabel}", new Vector2(panelX + 22, panelY + 170), 18, 1, new Color(217, 233, 241, 255));
        Raylib.DrawTextEx(_uiFont, $"Размер: {_selectedProvince.WidthInTiles}x{_selectedProvince.HeightInTiles} клетки", new Vector2(panelX + 22, panelY + 193), 18, 1, new Color(217, 233, 241, 255));

        if (_playerCharacter is not null && _model.IsProvinceOwnedByCharacter(_selectedProvince, _playerCharacter.Id))
        {
            Raylib.DrawTextEx(_uiFont, "Ваше владение", new Vector2(panelX + 250, panelY + 18), 18, 1, new Color(161, 228, 139, 255));
        }
    }

    private void DrawProvinceOwnerLine(float x, float y, Character? owner)
    {
        Raylib.DrawTextEx(_uiFont, "Владелец:", new Vector2(x, y), 18, 1, new Color(217, 233, 241, 255));

        if (owner is null)
        {
            return;
        }

        Vector2 iconPosition = new(x + 95, y + 2);
        Raylib.DrawRectangleV(iconPosition, new Vector2(18, 18), owner.BannerColor);
        Raylib.DrawRectangleLines((int)iconPosition.X, (int)iconPosition.Y, 18, 18, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, owner.FullName, new Vector2(x + 121, y), 18, 1, new Color(255, 235, 153, 255));
    }

    private void DrawCharacterPanel()
    {
        if (_selectedCharacter is null)
        {
            return;
        }

        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();
        int panelX = 70;
        int panelY = 70;
        int panelWidth = screenWidth - 140;
        int panelHeight = screenHeight - 140;

        Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(6, 10, 14, 180));
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(21, 28, 34, 244));
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelWidth, panelHeight), 4f, new Color(236, 224, 186, 255));

        DrawCharacterPortrait(panelX + 28, panelY + 34, 240, 320, _selectedCharacter);
        DrawCharacterHeader(panelX + 300, panelY + 34, _selectedCharacter);
        DrawCharacterSkills(panelX + 300, panelY + 118, _selectedCharacter);
        DrawCharacterTraits(panelX + 560, panelY + 118, _selectedCharacter);
        DrawCharacterFamily(panelX + 300, panelY + 330, _selectedCharacter);
        DrawCharacterResources(panelX + 300, panelY + panelHeight - 120, _selectedCharacter);
        DrawCharacterActions(panelX + 300, panelY + panelHeight - 70, _selectedCharacter);
        DrawCloseButton(panelX + panelWidth - 52, panelY + 18);
    }

    private void DrawCharacterPortrait(int x, int y, int width, int height, Character character)
    {
        Raylib.DrawRectangle(x, y, width, height, new Color(46, 57, 68, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, width, height), 3f, character.BannerColor);
        Raylib.DrawCircle(x + width / 2, y + 105, 58, character.PortraitColor);
        Raylib.DrawRectangleRounded(new Rectangle(x + 54, y + 176, 132, 110), 0.22f, 12, character.PortraitColor);
        Raylib.DrawTextEx(_uiFont, "Портрет", new Vector2(x + 70, y + height - 42), 24, 1, Color.RayWhite);
    }

    private void DrawCharacterHeader(int x, int y, Character character)
    {
        string lifeState = character.IsAlive ? "Жив" : "Мертв";
        Raylib.DrawTextEx(_uiFont, character.FullName, new Vector2(x, y), 30, 1, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, $"{character.Title} • {character.Age} лет • {character.Gender} • {lifeState}", new Vector2(x, y + 38), 20, 1, new Color(221, 229, 236, 255));
        Raylib.DrawRectangle(x, y + 68, 26, 16, character.BannerColor);
        Raylib.DrawRectangleLines(x, y + 68, 26, 16, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, $"Династия {character.HouseName}", new Vector2(x + 38, y + 62), 21, 1, new Color(235, 217, 158, 255));
    }

    private void DrawCharacterSkills(int x, int y, Character character)
    {
        DrawSkillLine(x, y, "Дипломатия", character.Skills.Diplomacy, new Color(108, 193, 117, 255));
        DrawSkillLine(x, y + 34, "Военное дело", character.Skills.Martial, new Color(194, 93, 93, 255));
        DrawSkillLine(x, y + 68, "Управление", character.Skills.Stewardship, new Color(108, 193, 117, 255));
        DrawSkillLine(x, y + 102, "Интриги", character.Skills.Intrigue, new Color(214, 185, 82, 255));
        DrawSkillLine(x, y + 136, "Учёность", character.Skills.Learning, new Color(135, 179, 215, 255));
    }

    private void DrawSkillLine(int x, int y, string label, int value, Color valueColor)
    {
        Raylib.DrawTextEx(_uiFont, label, new Vector2(x, y), 21, 1, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, value.ToString(), new Vector2(x + 190, y), 21, 1, valueColor);
    }

    private void DrawCharacterTraits(int x, int y, Character character)
    {
        Raylib.DrawTextEx(_uiFont, "Черты характера", new Vector2(x, y - 30), 22, 1, Color.RayWhite);

        for (int index = 0; index < character.Traits.Count; index++)
        {
            string trait = character.Traits[index];
            bool positive = !trait.Contains("Трус", StringComparison.Ordinal);
            string marker = positive ? "(+)" : "(-)";
            Color color = positive ? new Color(132, 201, 120, 255) : new Color(214, 122, 122, 255);
            Raylib.DrawTextEx(_uiFont, $"{trait} {marker}", new Vector2(x, y + index * 28), 18, 1, color);
        }
    }

    private void DrawCharacterFamily(int x, int y, Character character)
    {
        Raylib.DrawTextEx(_uiFont, "Семья", new Vector2(x, y), 24, 1, Color.RayWhite);

        DrawFamilyLine(GetFamilyLabel(character.SpouseId, "Муж", "Жена", "Супруг"), character.SpouseId, x, y + 36);
        DrawFamilyLine(GetFamilyLabel(character.ParentIds.ElementAtOrDefault(0), "Отец", "Мать", "Родитель"), character.ParentIds.ElementAtOrDefault(0), x, y + 72);
        DrawFamilyLine(GetFamilyLabel(character.ParentIds.ElementAtOrDefault(1), "Отец", "Мать", "Родитель"), character.ParentIds.ElementAtOrDefault(1), x, y + 104);
        DrawFamilyLine(GetFamilyLabel(character.ChildIds.ElementAtOrDefault(0), "Сын", "Дочь", "Ребёнок"), character.ChildIds.ElementAtOrDefault(0), x, y + 140);
        DrawFamilyLine(GetFamilyLabel(character.ChildIds.ElementAtOrDefault(1), "Сын", "Дочь", "Ребёнок"), character.ChildIds.ElementAtOrDefault(1), x, y + 172);
    }

    private void DrawFamilyLine(string label, int? characterId, int x, int y)
    {
        if (!string.IsNullOrEmpty(label))
        {
            Raylib.DrawTextEx(_uiFont, $"{label}:", new Vector2(x, y), 19, 1, new Color(217, 233, 241, 255));
        }

        if (characterId is null)
        {
            return;
        }

        Character? familyMember = _model.GetCharacterById(characterId.Value);
        if (familyMember is null)
        {
            return;
        }

        float labelOffset = string.IsNullOrEmpty(label) ? 0f : 96f;
        Raylib.DrawTextEx(_uiFont, familyMember.FullName, new Vector2(x + labelOffset, y), 19, 1, new Color(255, 235, 153, 255));
    }

    private void DrawCharacterResources(int x, int y, Character character)
    {
        DrawResourceChip(x, y, new Color(205, 165, 72, 255), $"Золото {character.Gold}");
        DrawResourceChip(x + 180, y, new Color(126, 171, 222, 255), $"Престиж {character.Prestige}");
        DrawResourceChip(x + 390, y, new Color(184, 215, 140, 255), $"Благочестие {character.Piety}");
    }

    private void DrawResourceChip(int x, int y, Color iconColor, string label)
    {
        Raylib.DrawCircle(x + 10, y + 11, 8, iconColor);
        Raylib.DrawTextEx(_uiFont, label, new Vector2(x + 24, y), 20, 1, Color.RayWhite);
    }

    private void DrawCharacterActions(int x, int y, Character character)
    {
        bool isPlayerCharacter = _playerCharacter?.Id == character.Id;

        if (isPlayerCharacter)
        {
            DrawActionButton(new Rectangle(x, y, 190, 38), "Найти супругу", false);
            DrawActionButton(new Rectangle(x + 206, y, 190, 38), "Назначить советников", false);
            DrawActionButton(new Rectangle(x + 412, y, 190, 38), "Воспитывать наследника", false);
            DrawActionButton(new Rectangle(x + 618, y, 190, 38), "Управлять доменом", false);
            DrawActionButton(new Rectangle(x + 824, y, 190, 38), "Открыть интриги", false);
            return;
        }

        DrawActionButton(new Rectangle(x, y, 190, 38), "Предложить брак", false);
        DrawActionButton(new Rectangle(x + 206, y, 190, 38), "Отправить подарок", false);
        DrawActionButton(new Rectangle(x + 412, y, 190, 38), "Предложить альянс", false);
        DrawActionButton(new Rectangle(x + 618, y, 190, 38), "Объявить войну", true);
        DrawActionButton(new Rectangle(x + 824, y, 190, 38), "Открыть интриги", false);
    }

    private void DrawRulerSelectionPanel()
    {
        if (!_isRulerSelectionOpen)
        {
            return;
        }

        int panelX = 250;
        int panelY = 110;
        int panelWidth = Raylib.GetScreenWidth() - 500;
        int panelHeight = Raylib.GetScreenHeight() - 220;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(6, 10, 14, 170));
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(21, 28, 34, 246));
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelWidth, panelHeight), 4f, new Color(236, 224, 186, 255));
        Raylib.DrawTextEx(_uiFont, "Выбор стартового правителя", new Vector2(panelX + 26, panelY + 20), 30, 1, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, "Клик по карточке открывает персонажа. Кнопка справа начинает правление.", new Vector2(panelX + 26, panelY + 56), 18, 1, new Color(217, 233, 241, 255));
        DrawCloseButton(panelX + panelWidth - 52, panelY + 18);

        foreach ((Character character, Rectangle cardRect, Rectangle buttonRect) in GetRulerCardRects())
        {
            DrawRulerCard(character, cardRect, buttonRect);
        }
    }

    private void DrawRulerCard(Character character, Rectangle cardRect, Rectangle buttonRect)
    {
        Province? province = _model.GetOwnedProvinces(character.Id).FirstOrDefault();

        Raylib.DrawRectangleRounded(cardRect, 0.08f, 8, new Color(35, 46, 56, 255));
        Raylib.DrawRectangleLinesEx(cardRect, 2f, character.BannerColor);
        Raylib.DrawRectangle((int)cardRect.X + 16, (int)cardRect.Y + 14, 56, 56, character.PortraitColor);
        Raylib.DrawRectangleLines((int)cardRect.X + 16, (int)cardRect.Y + 14, 56, 56, Color.RayWhite);

        Raylib.DrawTextEx(_uiFont, character.FullName, new Vector2(cardRect.X + 88, cardRect.Y + 12), 20, 1, Color.RayWhite);
        Raylib.DrawTextEx(_uiFont, $"Дом {character.HouseName}", new Vector2(cardRect.X + 88, cardRect.Y + 38), 17, 1, new Color(235, 217, 158, 255));
        Raylib.DrawTextEx(_uiFont, $"{character.Age} лет • {character.Gender}", new Vector2(cardRect.X + 88, cardRect.Y + 60), 16, 1, new Color(217, 233, 241, 255));

        string traitsPreview = string.Join(", ", character.Traits.Take(3));
        Raylib.DrawTextEx(_uiFont, traitsPreview, new Vector2(cardRect.X + 16, cardRect.Y + 86), 16, 1, new Color(166, 210, 160, 255));

        if (province is not null)
        {
            Raylib.DrawRectangle((int)cardRect.X + 16, (int)cardRect.Y + 112, 18, 18, _model.GetTileColor(province.TerrainType, province.StartTileX, province.StartTileY));
            Raylib.DrawRectangleLines((int)cardRect.X + 16, (int)cardRect.Y + 112, 18, 18, Color.RayWhite);
            Raylib.DrawTextEx(_uiFont, province.CountyName, new Vector2(cardRect.X + 44, cardRect.Y + 109), 16, 1, new Color(217, 233, 241, 255));
        }

        DrawActionButton(buttonRect, "Играть за него", false);
    }

    private void DrawActionButton(Rectangle rectangle, string label, bool disabled)
    {
        Color fillColor = disabled ? new Color(77, 80, 85, 255) : new Color(70, 88, 106, 255);
        Color textColor = disabled ? new Color(165, 170, 176, 255) : Color.RayWhite;
        Raylib.DrawRectangleRounded(rectangle, 0.18f, 8, fillColor);
        Raylib.DrawRectangleLinesEx(rectangle, 2f, new Color(231, 220, 184, 255));
        Raylib.DrawTextEx(_uiFont, label, new Vector2(rectangle.X + 12, rectangle.Y + 8), 18, 1, textColor);
    }

    private void DrawCloseButton(int x, int y)
    {
        Raylib.DrawRectangleRounded(new Rectangle(x, y, 28, 28), 0.2f, 6, new Color(78, 52, 52, 255));
        Raylib.DrawTextEx(_uiFont, "X", new Vector2(x + 7, y + 2), 22, 1, Color.RayWhite);
    }

    private void HandleLeftClick(Vector2 mousePosition)
    {
        if (_selectedCharacter is not null)
        {
            if (IsCloseButtonClicked(mousePosition))
            {
                _selectedCharacter = null;
                return;
            }

            int? familyCharacterId = GetClickedFamilyCharacterId(mousePosition);
            if (familyCharacterId is not null)
            {
                _selectedCharacter = _model.GetCharacterById(familyCharacterId.Value);
                return;
            }
        }

        if (_isRulerSelectionOpen)
        {
            if (IsRulerSelectionCloseClicked(mousePosition))
            {
                _isRulerSelectionOpen = false;
                return;
            }

            Character? chosenRuler = GetChosenRulerAtMouse(mousePosition);
            if (chosenRuler is not null)
            {
                _playerCharacter = chosenRuler;
                _selectedCharacter = chosenRuler;
                _selectedProvince = _model.GetOwnedProvinces(chosenRuler.Id).FirstOrDefault();
                _isRulerSelectionOpen = false;
                return;
            }

            Character? previewRuler = GetPreviewRulerAtMouse(mousePosition);
            if (previewRuler is not null)
            {
                _selectedCharacter = previewRuler;
                return;
            }
        }

        if (Raylib.CheckCollisionPointRec(mousePosition, GetChooseRulerButtonRect()))
        {
            _isRulerSelectionOpen = !_isRulerSelectionOpen;
            return;
        }

        if (_playerCharacter is not null && Raylib.CheckCollisionPointRec(mousePosition, GetPlayerPanelRect()))
        {
            _selectedCharacter = _playerCharacter;
            return;
        }

        Character? ownerFromProvincePanel = GetProvinceOwnerAtMouse(mousePosition);
        if (ownerFromProvincePanel is not null)
        {
            _selectedCharacter = ownerFromProvincePanel;
            return;
        }

        Vector2 mouseWorldPosition = Raylib.GetScreenToWorld2D(mousePosition, _camera);
        _selectedProvince = _model.GetProvinceAtWorldPosition(mouseWorldPosition);
    }

    private Character? GetProvinceOwnerAtMouse(Vector2 mousePosition)
    {
        if (_selectedProvince is null)
        {
            return null;
        }

        Character? owner = _model.GetProvinceOwner(_selectedProvince);
        if (owner is null)
        {
            return null;
        }

        Rectangle ownerRect = GetProvinceOwnerClickableRect(owner);
        return Raylib.CheckCollisionPointRec(mousePosition, ownerRect) ? owner : null;
    }

    private Character? GetChosenRulerAtMouse(Vector2 mousePosition)
    {
        foreach ((Character character, _, Rectangle buttonRect) in GetRulerCardRects())
        {
            if (Raylib.CheckCollisionPointRec(mousePosition, buttonRect))
            {
                return character;
            }
        }

        return null;
    }

    private Character? GetPreviewRulerAtMouse(Vector2 mousePosition)
    {
        foreach ((Character character, Rectangle cardRect, Rectangle buttonRect) in GetRulerCardRects())
        {
            if (Raylib.CheckCollisionPointRec(mousePosition, cardRect) && !Raylib.CheckCollisionPointRec(mousePosition, buttonRect))
            {
                return character;
            }
        }

        return null;
    }

    private IEnumerable<(Character Character, Rectangle CardRect, Rectangle ButtonRect)> GetRulerCardRects()
    {
        int panelX = 250;
        int panelY = 110;
        int columnWidth = 520;
        int rowHeight = 160;
        int gapX = 28;
        int gapY = 18;

        for (int index = 0; index < _selectableRulers.Count; index++)
        {
            int column = index % 2;
            int row = index / 2;
            int x = panelX + 26 + column * (columnWidth + gapX);
            int y = panelY + 100 + row * (rowHeight + gapY);
            Rectangle cardRect = new(x, y, columnWidth, rowHeight);
            Rectangle buttonRect = new(x + columnWidth - 176, y + 110, 160, 36);
            yield return (_selectableRulers[index], cardRect, buttonRect);
        }
    }

    private Rectangle GetProvinceOwnerClickableRect(Character owner)
    {
        const int panelWidth = 420;
        int panelX = Raylib.GetScreenWidth() - panelWidth - 22;
        int panelY = 22;
        Vector2 textSize = Raylib.MeasureTextEx(_uiFont, owner.FullName, 18, 1);
        return new Rectangle(panelX + 143, panelY + 118, textSize.X, textSize.Y + 4);
    }

    private int? GetClickedFamilyCharacterId(Vector2 mousePosition)
    {
        if (_selectedCharacter is null)
        {
            return null;
        }

        foreach ((int id, Rectangle rect) in GetFamilyClickableRects(_selectedCharacter))
        {
            if (Raylib.CheckCollisionPointRec(mousePosition, rect))
            {
                return id;
            }
        }

        return null;
    }

    private IEnumerable<(int Id, Rectangle Rect)> GetFamilyClickableRects(Character character)
    {
        int panelX = 70;
        int panelY = 70;
        int baseX = panelX + 300;
        int baseY = panelY + 330;

        return BuildFamilyClickableRects(character, baseX, baseY)
            .Where(item => item.Rect.Width > 0 && item.Rect.Height > 0);
    }

    private IEnumerable<(int Id, Rectangle Rect)> BuildFamilyClickableRects(Character character, int x, int y)
    {
        return new[]
        {
            CreateFamilyRect(character.SpouseId, x + 96, y + 36),
            CreateFamilyRect(character.ParentIds.ElementAtOrDefault(0), x + 116, y + 72),
            CreateFamilyRect(character.ParentIds.ElementAtOrDefault(1), x + 116, y + 104),
            CreateFamilyRect(character.ChildIds.ElementAtOrDefault(0), x + 108, y + 140),
            CreateFamilyRect(character.ChildIds.ElementAtOrDefault(1), x + 108, y + 172)
        };
    }

    private (int Id, Rectangle Rect) CreateFamilyRect(int? characterId, float x, float y)
    {
        if (characterId is null)
        {
            return (0, new Rectangle());
        }

        Character? character = _model.GetCharacterById(characterId.Value);
        if (character is null)
        {
            return (0, new Rectangle());
        }

        Vector2 size = Raylib.MeasureTextEx(_uiFont, character.FullName, 19, 1);
        return (characterId.Value, new Rectangle(x, y, size.X, size.Y + 4));
    }

    private bool IsCloseButtonClicked(Vector2 mousePosition)
    {
        int screenWidth = Raylib.GetScreenWidth();
        int panelX = 70;
        int panelWidth = screenWidth - 140;
        Rectangle closeRect = new(panelX + panelWidth - 52, 88, 28, 28);
        return Raylib.CheckCollisionPointRec(mousePosition, closeRect);
    }

    private bool IsRulerSelectionCloseClicked(Vector2 mousePosition)
    {
        int panelX = 250;
        int panelWidth = Raylib.GetScreenWidth() - 500;
        Rectangle closeRect = new(panelX + panelWidth - 52, 128, 28, 28);
        return Raylib.CheckCollisionPointRec(mousePosition, closeRect);
    }

    private string GetFamilyLabel(int? characterId, string maleLabel, string femaleLabel, string fallbackLabel)
    {
        if (characterId is null)
        {
            return fallbackLabel;
        }

        Character? familyMember = _model.GetCharacterById(characterId.Value);
        if (familyMember is null)
        {
            return fallbackLabel;
        }

        return familyMember.Gender == "Женщина" ? femaleLabel : maleLabel;
    }

    private static float GetAxis(KeyboardKey negativeMain, KeyboardKey negativeAlt, KeyboardKey positiveMain, KeyboardKey positiveAlt)
    {
        float axis = 0f;

        if (Raylib.IsKeyDown(negativeMain) || Raylib.IsKeyDown(negativeAlt))
        {
            axis -= 1f;
        }

        if (Raylib.IsKeyDown(positiveMain) || Raylib.IsKeyDown(positiveAlt))
        {
            axis += 1f;
        }

        return axis;
    }

    private static Rectangle GetChooseRulerButtonRect()
    {
        return new Rectangle(18, 304, 340, 48);
    }

    private static Rectangle GetPlayerPanelRect()
    {
        return new Rectangle(18, 186, 340, 102);
    }
}
