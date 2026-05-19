namespace NmeaAisParser.Models
{
    // ─────────────────────────────────────────────────────────────
    //  Generic NMEA sentence result
    // ─────────────────────────────────────────────────────────────
    public class NmeaParseResult
    {
        public bool IsValid { get; set; }
        public string SentenceType { get; set; } = "";
        public string RawSentence { get; set; } = "";
        public string TalkerID { get; set; } = "";
        public string MessageType { get; set; } = "";
        public Dictionary<string, string> Fields { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string Checksum { get; set; } = "";
        public bool ChecksumValid { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS Type 1 – Position Report Class A
    // ─────────────────────────────────────────────────────────────
    public class AisType1Input
    {
        public int MessageType { get; set; } = 1;
        public int RepeatIndicator { get; set; } = 0;
        public int MMSI { get; set; } = 123456789;
        public int NavigationStatus { get; set; } = 0;   // 0=Under way using engine
        public int RateOfTurn { get; set; } = 0;          // -128=no info
        public double SpeedOverGround { get; set; } = 0.0;
        public int PositionAccuracy { get; set; } = 0;
        public double Longitude { get; set; } = 0.0;
        public double Latitude { get; set; } = 0.0;
        public double CourseOverGround { get; set; } = 0.0;
        public int TrueHeading { get; set; } = 511;       // 511=not available
        public int TimeStamp { get; set; } = 60;           // 60=not available
        public int ManeuverIndicator { get; set; } = 0;
        public int RAIMFlag { get; set; } = 0;
    }

    public class AisType1Result
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string NmeaSentence { get; set; } = "";
        public string BinaryPayload { get; set; } = "";
        public string EncodedPayload { get; set; } = "";

        // Decoded fields
        public int MessageType { get; set; }
        public int RepeatIndicator { get; set; }
        public int MMSI { get; set; }
        public string NavigationStatus { get; set; } = "";
        public int RateOfTurn { get; set; }
        public double SpeedOverGround { get; set; }
        public int PositionAccuracy { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double CourseOverGround { get; set; }
        public int TrueHeading { get; set; }
        public int TimeStamp { get; set; }
        public int ManeuverIndicator { get; set; }
        public int RAIMFlag { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS Type 5 – Static and Voyage Related Data
    // ─────────────────────────────────────────────────────────────
    public class AisType5Input
    {
        public int MessageType { get; set; } = 5;
        public int RepeatIndicator { get; set; } = 0;
        public int MMSI { get; set; } = 123456789;
        public int AisVersion { get; set; } = 0;
        public int IMONumber { get; set; } = 0;
        public string CallSign { get; set; } = "CALLSGN";
        public string VesselName { get; set; } = "VESSEL NAME";
        public int ShipType { get; set; } = 70;       // 70 = Cargo
        public int DimensionToBow { get; set; } = 0;
        public int DimensionToStern { get; set; } = 0;
        public int DimensionToPort { get; set; } = 0;
        public int DimensionToStarboard { get; set; } = 0;
        public int TypeOfEPFD { get; set; } = 1;      // 1 = GPS
        public int ETAMonth { get; set; } = 0;
        public int ETADay { get; set; } = 0;
        public int ETAHour { get; set; } = 24;
        public int ETAMinute { get; set; } = 60;
        public double MaxDraught { get; set; } = 0.0;
        public string Destination { get; set; } = "DESTINATION";
        public int DTE { get; set; } = 0;
    }

    public class AisType5Result
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> NmeaSentences { get; set; } = new();
        public string BinaryPayload { get; set; } = "";
        public string EncodedPayload { get; set; } = "";

        // Decoded fields
        public int MessageType { get; set; }
        public int RepeatIndicator { get; set; }
        public int MMSI { get; set; }
        public int AisVersion { get; set; }
        public int IMONumber { get; set; }
        public string CallSign { get; set; } = "";
        public string VesselName { get; set; } = "";
        public string ShipType { get; set; } = "";
        public int DimensionToBow { get; set; }
        public int DimensionToStern { get; set; }
        public int DimensionToPort { get; set; }
        public int DimensionToStarboard { get; set; }
        public string TypeOfEPFD { get; set; } = "";
        public string ETA { get; set; } = "";
        public double MaxDraught { get; set; }
        public string Destination { get; set; } = "";
        public int DTE { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  View models
    // ─────────────────────────────────────────────────────────────
    public class HomeViewModel
    {
        public string? NmeaInput { get; set; }
        public NmeaParseResult? NmeaResult { get; set; }

        public string? AisParseInput { get; set; }
        public object? AisParseResult { get; set; }
        public string? AisParseType { get; set; }

        public AisType1Input Type1Input { get; set; } = new();
        public AisType1Result? Type1Result { get; set; }

        public AisType5Input Type5Input { get; set; } = new();
        public AisType5Result? Type5Result { get; set; }

        public string ActiveTab { get; set; } = "nmea";
    }
}
