// File: MetromontCastLink.Shared/Models/EngineeringModels.cs
// ADD this new file to MetromontCastLink.Shared/Models/

using System;
using System.Collections.Generic;

namespace MetromontCastLink.Shared.Models
{
    // BIM Element Model - NEW
    public class BIMElement
    {
        public int Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    // Point Load Calculation - NEW
    public class PointLoadCalculation
    {
        public string BearingPiece { get; set; } = string.Empty;
        public double LoadMagnitude { get; set; }
        public double LoadDistance { get; set; }
        public double BeamLength { get; set; }
        public double Eccentricity { get; set; }
        public List<BIMElement> ContributingElements { get; set; } = new();
    }

    // Enhanced Column Calculation for PCI - NEW
    public class PCIColumnCalculation : ColumnCalculationData
    {
        public double PrestressLevel { get; set; } = 225; // PCI minimum
        public int NumberOfTies { get; set; } = 4;
        public double TieCapacity { get; set; } = 10000; // lb
    }

    // Enhanced Double Tee Calculation for PCI - NEW
    public class PCIDoubleTeeCalculation : DoubleTeeCalculationData
    {
        public double ToppingThickness { get; set; } = 2; // in
        public bool RequiresShearReinforcement { get; set; }
        public double SuperimposedLoad { get; set; } = 15; // psf
    }

    // Flat Slab Calculation - NEW
    public class FlatSlabCalculation
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double Thickness { get; set; }
        public double SpanLength { get; set; }
        public double LiveLoad { get; set; } = 50; // psf
        public double ConcreteStrength { get; set; } = 5000; // psi
        public int StrandPattern { get; set; } // Number of strands
    }

    // H-Frame Calculation - NEW
    public class HFrameCalculation
    {
        public double ColumnSpacing { get; set; }
        public double BeamDepth { get; set; }
        public double ColumnWidth { get; set; }
        public double ColumnDepth { get; set; }
        public double Height { get; set; }
        public double LateralLoad { get; set; }
        public double GravityLoad { get; set; }
    }

    // Hollow Core Slab Calculation - NEW
    public class HollowCoreCalculation
    {
        public double Width { get; set; } = 4; // ft
        public double Depth { get; set; } = 8; // in
        public double SpanLength { get; set; }
        public double LiveLoad { get; set; } = 50; // psf
        public double ToppingThickness { get; set; } = 2; // in
        public bool CompositeAction { get; set; } = true;
    }

    // L-Girder Calculation - NEW
    public class LGirderCalculation
    {
        public double LedgeWidth { get; set; }
        public double WebHeight { get; set; }
        public double WebThickness { get; set; }
        public double BottomFlangeWidth { get; set; }
        public double SpanLength { get; set; }
        public double UniformLoad { get; set; }
        public int NumberOfStrands { get; set; }
    }

    // Metrodeck Calculation - NEW
    public class MetrodeckCalculation
    {
        public double DeckWidth { get; set; }
        public double DeckDepth { get; set; }
        public double SpanLength { get; set; }
        public double LiveLoad { get; set; } = 50; // psf
        public double ConcreteTopping { get; set; } = 3; // in
        public string FireRating { get; set; } = "2-hour";
    }

    // Metrowall Calculation - NEW
    public class MetrowallCalculation
    {
        public double WallHeight { get; set; }
        public double WallLength { get; set; }
        public double WallThickness { get; set; }
        public double AxialLoad { get; set; }
        public double LateralLoad { get; set; }
        public bool IsLoadBearing { get; set; } = true;
        public string InsulationType { get; set; } = "R-15";
    }

    // R-Beam Calculation - NEW
    public class RBeamCalculation : BeamCalculationData
    {
        public double ConcentratedLoad { get; set; }
        public double LoadLocation { get; set; }
        public int NumberOfStrands { get; set; }
        public double StrandEccentricity { get; set; }
    }

    // Shearwall Calculation - NEW
    public class ShearwallCalculation
    {
        public double WallHeight { get; set; }
        public double WallLength { get; set; }
        public double WallThickness { get; set; }
        public double ShearForce { get; set; }
        public double OverturningMoment { get; set; }
        public double AxialLoad { get; set; }
        public string ReinforcementType { get; set; } = "Vertical bars";
    }

    // Spandrel Calculation - NEW
    public class SpandrelCalculation : BeamCalculationData
    {
        public double TorsionalLoad { get; set; }
        public bool IsArchitectural { get; set; }
        public string FinishType { get; set; } = "Exposed aggregate";
    }

    // Stair Calculation - NEW
    public class StairCalculation
    {
        public double TotalRise { get; set; }
        public double TotalRun { get; set; }
        public double TreadDepth { get; set; } = 11; // in
        public double RiserHeight { get; set; } = 7; // in
        public double StairWidth { get; set; } = 4; // ft
        public double LiveLoad { get; set; } = 100; // psf
        public int NumberOfLandings { get; set; }
    }

    // T-Girder Calculation - NEW
    public class TGirderCalculation : BeamCalculationData
    {
        public double FlangeWidth { get; set; }
        public double FlangeThickness { get; set; }
        public double WebHeight { get; set; }
        public double WebThickness { get; set; }
        public int NumberOfStrands { get; set; }
        public double PrestressForce { get; set; }
    }

    // Wall Panel Calculation - NEW
    public class WallPanelCalculation
    {
        public double Height { get; set; }
        public double Width { get; set; }
        public double Thickness { get; set; }
        public double LateralLoad { get; set; } = 20; // psf
        public double AxialLoad { get; set; }
        public int NumberOfTies { get; set; } = 2; // PCI minimum
        public double TieCapacity { get; set; } = 10000; // lb, PCI minimum
        public bool HasOpenings { get; set; }
        public string InsulationType { get; set; } = "Sandwich panel";
    }

    // PCI Design Parameters - NEW
    public static class PCIDesignParameters
    {
        public const double MinimumPrestress = 225; // psi
        public const double MinimumColumnTieFactor = 200; // Ag lb
        public const double MinimumWallTieCapacity = 10000; // lb
        public const int MinimumWallTies = 2;

        // Load factors
        public const double DeadLoadFactor = 1.2;
        public const double LiveLoadFactor = 1.6;
        public const double WindLoadFactor = 1.0;

        // Phi factors
        public const double PhiFlexure = 0.9;
        public const double PhiShear = 0.75;
        public const double PhiCompression = 0.65;
        public const double PhiTension = 0.9;

        // Concrete properties
        public const double ConcreteUnitWeight = 150; // pcf
        public const double LightweightConcreteUnitWeight = 110; // pcf

        // Prestressing steel properties
        public const double StrandUltimateStrength = 270; // ksi
        public const double StrandYieldStrength = 243; // ksi
        public const double StrandModulus = 28500; // ksi
    }

    // Material Properties - NEW
    public class MaterialProperties
    {
        public string Name { get; set; } = string.Empty;
        public double Density { get; set; } // pcf
        public double CompressiveStrength { get; set; } // psi
        public double ModulusOfElasticity { get; set; } // psi
        public double PoissonRatio { get; set; }

        public static Dictionary<string, MaterialProperties> StandardMaterials = new()
        {
            ["Concrete-5000"] = new MaterialProperties
            {
                Name = "5000 psi Concrete",
                Density = 150,
                CompressiveStrength = 5000,
                ModulusOfElasticity = 57000 * Math.Sqrt(5000),
                PoissonRatio = 0.2
            },
            ["Concrete-6000"] = new MaterialProperties
            {
                Name = "6000 psi Concrete",
                Density = 150,
                CompressiveStrength = 6000,
                ModulusOfElasticity = 57000 * Math.Sqrt(6000),
                PoissonRatio = 0.2
            },
            ["Concrete-8000"] = new MaterialProperties
            {
                Name = "8000 psi Concrete",
                Density = 150,
                CompressiveStrength = 8000,
                ModulusOfElasticity = 57000 * Math.Sqrt(8000),
                PoissonRatio = 0.2
            },
            ["LightweightConcrete"] = new MaterialProperties
            {
                Name = "Lightweight Concrete",
                Density = 110,
                CompressiveStrength = 4000,
                ModulusOfElasticity = 33 * Math.Pow(110, 1.5) * Math.Sqrt(4000),
                PoissonRatio = 0.2
            }
        };
    }
}