using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Origami.Runtime.Systems
{
    /// <summary>
    /// The only place with Resources keys of the imported art (ARCHITECTURE §14.2, C-195, <c>Art/ASSETS.md</c>).
    /// Key = <c>"&lt;Set&gt;/&lt;Name&gt;"</c> without extension. Models load with <c>Resources.Load&lt;GameObject&gt;</c>
    /// (<see cref="ModelFit"/>), textures with <c>Resources.Load&lt;Texture2D&gt;</c>.
    /// </summary>
    public static class ArtKeys
    {
        // Workshop set (Kenney Furniture Kit, CC0).
        public const string Table = "Workshop/Table";
        public const string Chair = "Workshop/Chair";
        public const string Bookshelf = "Workshop/Bookshelf";
        public const string Books = "Workshop/Books";
        public const string DisplayShelf = "Workshop/DisplayShelf";
        public const string Bed = "Workshop/Bed";
        public const string Nightstand = "Workshop/Nightstand";
        public const string TrashCan = "Workshop/TrashCan";
        public const string Door = "Workshop/Door";
        public const string Window = "Workshop/Window";
        public const string LampTable = "Workshop/LampTable";
        public const string LampFloor = "Workshop/LampFloor";
        public const string Rug = "Workshop/Rug";
        public const string PottedPlant = "Workshop/PottedPlant";
        public const string PlantSmall = "Workshop/PlantSmall";
        public const string Box = "Workshop/Box";
        public const string Paneling = "Workshop/Paneling";
        public const string CoatRack = "Workshop/CoatRack";

        // Characters set (Quaternius Ultimate Animated Character Pack, CC0).
        public const string Crafter = "Characters/Crafter";

        // Surfaces set (ambientCG, CC0). Not used by the room by default: floor and walls are flat colour (DECISIONS 2026-11-28).
        public const string WoodFloor = "Surfaces/WoodFloor";
        public const string PlasterWall = "Surfaces/PlasterWall";

        /// <summary>Legacy clip names of <see cref="Crafter"/> (C-195: the take name after the last '|').</summary>
        public const string ClipIdle = "Idle", ClipWalk = "Walk", ClipSitDown = "SitDown", ClipStandUp = "StandUp";

        /// <summary>
        /// Yaw (degrees) used for the Crafter's front when it cannot be measured from the foot bones at runtime
        /// (ARCHITECTURE §14.7: front expected at +z, so 0).
        /// </summary>
        public const float CrafterFrontYawFallback = 0f;

        /// <summary>Material name (case-insensitive substring) of the window panes in <see cref="Window"/>.</summary>
        public const string WindowGlassMaterial = "glass";

        /// <summary>All 19 model keys (18 workshop models + the character), in ASSETS.md order.</summary>
        public static readonly IReadOnlyList<string> Models = new ReadOnlyCollection<string>(new[]
        {
            Table, Chair, Bookshelf, Books, DisplayShelf, Bed, Nightstand, TrashCan, Door, Window,
            LampTable, LampFloor, Rug, PottedPlant, PlantSmall, Box, Paneling, CoatRack, Crafter,
        });

        /// <summary>Both texture keys.</summary>
        public static readonly IReadOnlyList<string> Textures = new ReadOnlyCollection<string>(new[] { WoodFloor, PlasterWall });
    }
}
