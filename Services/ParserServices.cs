using NmeaAisParser.Models;
using System.Text;

namespace NmeaAisParser.Services
{
    // ─────────────────────────────────────────────────────────────
    //  NMEA sentence parser
    // ─────────────────────────────────────────────────────────────
    public static class NmeaParser
    {
        private static readonly Dictionary<string, string[]> FieldNames = new()
        {
            ["GGA"] = new[] { "UTC Time", "Latitude", "N/S", "Longitude", "E/W", "Fix Quality", "Satellites", "HDOP", "Altitude", "Alt Unit", "Geoid Sep", "Geoid Unit", "Age DGPS", "DGPS Station" },
            ["RMC"] = new[] { "UTC Time", "Status", "Latitude", "N/S", "Longitude", "E/W", "Speed (knots)", "Track Angle", "Date", "Magnetic Var", "Mag Var Dir", "Mode" },
            ["GLL"] = new[] { "Latitude", "N/S", "Longitude", "E/W", "UTC Time", "Status", "Mode" },
            ["GSA"] = new[] { "Mode 1", "Mode 2", "SV1", "SV2", "SV3", "SV4", "SV5", "SV6", "SV7", "SV8", "SV9", "SV10", "SV11", "SV12", "PDOP", "HDOP", "VDOP" },
            ["VTG"] = new[] { "Track (True)", "T", "Track (Mag)", "M", "Speed (knots)", "N", "Speed (km/h)", "K", "Mode" },
            ["ZDA"] = new[] { "UTC Time", "Day", "Month", "Year", "Local Zone Hours", "Local Zone Minutes" },
            // VDM / VDO: payload is always index 4 (5th data field after tag)
            ["VDM"] = new[] { "Count", "Number", "Seq ID", "Channel", "Payload", "Fill Bits" },
            ["VDO"] = new[] { "Count", "Number", "Seq ID", "Channel", "Payload", "Fill Bits" },
        };

        public static NmeaParseResult Parse(string raw)
        {
            var result = new NmeaParseResult { RawSentence = raw };
            try
            {
                raw = raw.Trim();
                if (string.IsNullOrEmpty(raw))
                { result.ErrorMessage = "Empty sentence."; return result; }

                if (!raw.StartsWith('$') && !raw.StartsWith('!'))
                { result.ErrorMessage = "Sentence must start with '$' or '!'."; return result; }

                // Extract checksum
                int star = raw.LastIndexOf('*');
                if (star > 0 && star < raw.Length - 1)
                {
                    string provided = raw[(star + 1)..].Trim().ToUpper();
                    string computed = Checksum(raw[1..star]);
                    result.Checksum = computed;
                    result.ChecksumValid = provided == computed;
                    raw = raw[..star];        // strip checksum for field parsing
                }
                else
                {
                    result.Checksum = Checksum(raw[1..]);
                    result.ChecksumValid = false;
                }

                string body = raw[1..];       // strip $ / !
                string[] parts = body.Split(',');

                string tag = parts[0];
                if (tag.Length >= 5)
                {
                    result.TalkerID = tag[..2];
                    result.MessageType = tag[2..];
                }
                else
                {
                    result.MessageType = tag;
                }
                result.SentenceType = tag;

                string[] names = FieldNames.TryGetValue(result.MessageType, out var n) ? n : Array.Empty<string>();

                for (int i = 1; i < parts.Length; i++)
                {
                    string key = (i - 1) < names.Length ? names[i - 1] : $"Field {i}";
                    result.Fields[key] = parts[i];
                }

                result.IsValid = true;
            }
            catch (Exception ex) { result.ErrorMessage = ex.Message; }
            return result;
        }

        public static string Checksum(string data)
        {
            byte cs = 0;
            foreach (char c in data) cs ^= (byte)c;
            return cs.ToString("X2");
        }

        /// <summary>
        /// Safely extract AIS payload from a parsed NMEA result.
        /// VDM sentences: !AIVDM,count,num,seq,ch,PAYLOAD,fillbits
        /// The payload is always the 5th data field (index 4, key "Payload").
        /// Falls back to positional lookup and longest-value heuristic.
        /// </summary>
        public static (string payload, int fillBits) ExtractPayloadAndFill(NmeaParseResult nmea)
        {
            // Primary: named key set by VDM/VDO field map
            if (nmea.Fields.TryGetValue("Payload", out var p) && !string.IsNullOrEmpty(p))
            {
                int fill = 0;
                if (nmea.Fields.TryGetValue("Fill Bits", out var fb)) int.TryParse(fb, out fill);
                return (p, fill);
            }

            // Fallback: positional — payload = Field 5, fill = Field 6
            if (nmea.Fields.TryGetValue("Field 5", out var f5) && !string.IsNullOrEmpty(f5))
            {
                int fill = 0;
                if (nmea.Fields.TryGetValue("Field 6", out var fb)) int.TryParse(fb, out fill);
                return (f5, fill);
            }

            // Last resort: raw split — payload is always the 6th comma-field (index 5) of the full sentence
            var parts = nmea.RawSentence.TrimStart('!', '$').Split(',');
            if (parts.Length >= 6 && !string.IsNullOrEmpty(parts[5]))
            {
                int fill = 0;
                if (parts.Length >= 7)
                {
                    string fb = parts[6].Split('*')[0];  // strip any trailing checksum
                    int.TryParse(fb, out fill);
                }
                return (parts[5], fill);
            }

            throw new InvalidOperationException("Cannot find AIS payload in sentence.");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS bit-level codec
    // ─────────────────────────────────────────────────────────────
    public static class AisBits
    {
        public static void WriteUInt(List<int> b, uint v, int n) { for (int i = n - 1; i >= 0; i--) b.Add((int)((v >> i) & 1)); }
        public static void WriteInt(List<int> b, int v, int n) { WriteUInt(b, (uint)(v & ((1 << n) - 1)), n); }

        public static void WriteText(List<int> b, string text, int charCount)
        {
            text = text.ToUpper().PadRight(charCount)[..charCount];
            foreach (char c in text)
            {
                int v = c switch
                {
                    '@' => 0,
                    >= 'A' and <= 'Z' => c - 'A' + 1,
                    >= '0' and <= '9' => c - '0' + 48,
                    ' ' => 32,
                    _ => 32
                };
                WriteUInt(b, (uint)v, 6);
            }
        }

        public static (string payload, int fillBits) Encode(List<int> bits)
        {
            int fill = (6 - bits.Count % 6) % 6;
            var padded = new List<int>(bits);
            for (int i = 0; i < fill; i++) padded.Add(0);

            var sb = new StringBuilder();
            for (int i = 0; i < padded.Count; i += 6)
            {
                int v = 0;
                for (int j = 0; j < 6; j++) v = (v << 1) | padded[i + j];
                v += 48;
                if (v > 87) v += 8;
                sb.Append((char)v);
            }
            return (sb.ToString(), fill);
        }

        public static List<int> Decode(string payload, int fillBits = 0)
        {
            var bits = new List<int>();
            foreach (char c in payload)
            {
                int v = c - 48;
                if (v > 39) v -= 8;
                for (int i = 5; i >= 0; i--) bits.Add((v >> i) & 1);
            }
            // Remove fill padding from the end
            if (fillBits > 0 && bits.Count >= fillBits)
                bits.RemoveRange(bits.Count - fillBits, fillBits);
            return bits;
        }

        public static uint ReadUInt(List<int> b, int s, int n)
        { uint v = 0; for (int i = 0; i < n; i++) v = (v << 1) | (uint)b[s + i]; return v; }

        public static int ReadInt(List<int> b, int s, int n)
        {
            uint u = ReadUInt(b, s, n);
            if ((u & (1u << (n - 1))) != 0) u |= ~((1u << n) - 1u);
            return (int)u;
        }

        public static string ReadText(List<int> b, int s, int charCount)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < charCount; i++)
            {
                uint v = ReadUInt(b, s + i * 6, 6);
                char c = (char)(v switch
                {
                    0 => '@',
                    >= 1 and <= 26 => (int)v + 'A' - 1,
                    32 => ' ',
                    >= 48 and <= 57 => (int)v + '0' - 48,
                    _ => ' '
                });
                sb.Append(c);
            }
            return sb.ToString().TrimEnd();
        }

        public static string BuildNmea(string payload, int fillBits, int part, int total, int seqId, char channel)
        {
            string body = $"AIVDM,{total},{part},{(seqId > 0 ? seqId.ToString() : "")},{channel},{payload},{fillBits}";
            return $"!{body}*{NmeaParser.Checksum(body)}";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS Type 1 – Position Report Class A
    // ─────────────────────────────────────────────────────────────
    public static class AisType1Service
    {
        static readonly string[] NavStatus =
        {
            "Under way using engine","At anchor","Not under command","Restricted manoeuvrability",
            "Constrained by her draught","Moored","Aground","Engaged in fishing","Under way sailing",
            "Reserved (9)","Reserved (10)","Reserved (11)","Reserved (12)","Reserved (13)",
            "AIS-SART active","Undefined"
        };

        public static AisType1Result Generate(AisType1Input inp)
        {
            var res = new AisType1Result();
            try
            {
                var b = new List<int>();
                AisBits.WriteUInt(b, (uint)inp.MessageType, 6);
                AisBits.WriteUInt(b, (uint)inp.RepeatIndicator, 2);
                AisBits.WriteUInt(b, (uint)inp.MMSI, 30);
                AisBits.WriteUInt(b, (uint)inp.NavigationStatus, 4);
                AisBits.WriteInt(b, inp.RateOfTurn, 8);
                AisBits.WriteUInt(b, (uint)Math.Round(inp.SpeedOverGround * 10), 10);
                AisBits.WriteUInt(b, (uint)inp.PositionAccuracy, 1);
                AisBits.WriteInt(b, (int)Math.Round(inp.Longitude * 600000), 28);
                AisBits.WriteInt(b, (int)Math.Round(inp.Latitude * 600000), 27);
                AisBits.WriteUInt(b, (uint)Math.Round(inp.CourseOverGround * 10), 12);
                AisBits.WriteUInt(b, (uint)inp.TrueHeading, 9);
                AisBits.WriteUInt(b, (uint)inp.TimeStamp, 6);
                AisBits.WriteUInt(b, (uint)inp.ManeuverIndicator, 2);
                AisBits.WriteUInt(b, 0, 3);   // spare
                AisBits.WriteUInt(b, (uint)inp.RAIMFlag, 1);
                AisBits.WriteUInt(b, 0, 19);  // radio status

                var (payload, fill) = AisBits.Encode(b);
                res.NmeaSentence = AisBits.BuildNmea(payload, fill, 1, 1, 0, 'A');
                res.EncodedPayload = payload;
                res.BinaryPayload = string.Concat(b);
                res.IsValid = true;
                PopulateDecoded(res, b);
            }
            catch (Exception ex) { res.ErrorMessage = ex.Message; }
            return res;
        }

        public static AisType1Result Parse(string sentence)
        {
            var res = new AisType1Result();
            try
            {
                sentence = sentence.Trim();
                if (string.IsNullOrEmpty(sentence))
                { res.ErrorMessage = "Empty sentence."; return res; }

                var nmea = NmeaParser.Parse(sentence);
                if (!nmea.IsValid)
                { res.ErrorMessage = $"NMEA error: {nmea.ErrorMessage}"; return res; }

                var (payload, fillBits) = NmeaParser.ExtractPayloadAndFill(nmea);

                if (string.IsNullOrEmpty(payload))
                { res.ErrorMessage = "No payload found in sentence."; return res; }

                var bits = AisBits.Decode(payload, fillBits);

                if (bits.Count < 168)
                { res.ErrorMessage = $"Payload too short: {bits.Count} bits (need 168). Check the sentence is a complete Type 1/2/3 message."; return res; }

                int msgType = (int)AisBits.ReadUInt(bits, 0, 6);
                if (msgType is not (1 or 2 or 3))
                { res.ErrorMessage = $"This is a Type {msgType} message, not Type 1/2/3. Use AIS Parser tab instead."; return res; }

                res.EncodedPayload = payload;
                res.BinaryPayload = string.Concat(bits);
                res.NmeaSentence = sentence;
                res.IsValid = true;
                PopulateDecoded(res, bits);
            }
            catch (Exception ex) { res.ErrorMessage = $"Parse error: {ex.Message}"; }
            return res;
        }

        static void PopulateDecoded(AisType1Result res, List<int> b)
        {
            res.MessageType = (int)AisBits.ReadUInt(b, 0, 6);
            res.RepeatIndicator = (int)AisBits.ReadUInt(b, 6, 2);
            res.MMSI = (int)AisBits.ReadUInt(b, 8, 30);
            int nav = (int)AisBits.ReadUInt(b, 38, 4);
            res.NavigationStatus = nav < NavStatus.Length ? NavStatus[nav] : "Unknown";
            res.RateOfTurn = AisBits.ReadInt(b, 42, 8);
            res.SpeedOverGround = AisBits.ReadUInt(b, 50, 10) / 10.0;
            res.PositionAccuracy = (int)AisBits.ReadUInt(b, 60, 1);
            res.Longitude = AisBits.ReadInt(b, 61, 28) / 600000.0;
            res.Latitude = AisBits.ReadInt(b, 89, 27) / 600000.0;
            res.CourseOverGround = AisBits.ReadUInt(b, 116, 12) / 10.0;
            res.TrueHeading = (int)AisBits.ReadUInt(b, 128, 9);
            res.TimeStamp = (int)AisBits.ReadUInt(b, 137, 6);
            res.ManeuverIndicator = (int)AisBits.ReadUInt(b, 143, 2);
            res.RAIMFlag = (int)AisBits.ReadUInt(b, 148, 1);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS Type 5 – Static and Voyage Related Data
    // ─────────────────────────────────────────────────────────────
    public static class AisType5Service
    {
        static readonly string[] ShipTypes =
        {
            "Not available","","","","","","","","","","","","","","","","","","","",
            "Wing in ground","WIG Hazard A","WIG Hazard B","WIG Hazard C","WIG Hazard D","","","","","",
            "Fishing","Towing","Towing >200m","Dredging","Diving","Military","Sailing","Pleasure Craft","","",
            "HSC","HSC Hazard A","HSC Hazard B","HSC Hazard C","HSC Hazard D","","","","","HSC No info",
            "Pilot Vessel","SAR","Tug","Port Tender","Anti-pollution","Law Enforcement","","","Medical","",
            "Passenger","Passenger Hazard A","Passenger Hazard B","Passenger Hazard C","Passenger Hazard D","","","","","Passenger No info",
            "Cargo","Cargo Hazard A","Cargo Hazard B","Cargo Hazard C","Cargo Hazard D","","","","","Cargo No info",
            "Tanker","Tanker Hazard A","Tanker Hazard B","Tanker Hazard C","Tanker Hazard D","","","","","Tanker No info",
            "Other","Other Hazard A","Other Hazard B","Other Hazard C","Other Hazard D","","","","","Other No info"
        };

        static readonly string[] EpfdTypes =
            { "Undefined","GPS","GLONASS","Combined GPS/GLONASS","Loran-C","Chayka","Integrated Nav","Surveyed","Galileo" };

        public static AisType5Result Generate(AisType5Input inp)
        {
            var res = new AisType5Result();
            try
            {
                var b = new List<int>();
                AisBits.WriteUInt(b, (uint)inp.MessageType, 6);
                AisBits.WriteUInt(b, (uint)inp.RepeatIndicator, 2);
                AisBits.WriteUInt(b, (uint)inp.MMSI, 30);
                AisBits.WriteUInt(b, (uint)inp.AisVersion, 2);
                AisBits.WriteUInt(b, (uint)inp.IMONumber, 30);
                AisBits.WriteText(b, inp.CallSign, 7);
                AisBits.WriteText(b, inp.VesselName, 20);
                AisBits.WriteUInt(b, (uint)inp.ShipType, 8);
                AisBits.WriteUInt(b, (uint)inp.DimensionToBow, 9);
                AisBits.WriteUInt(b, (uint)inp.DimensionToStern, 9);
                AisBits.WriteUInt(b, (uint)inp.DimensionToPort, 6);
                AisBits.WriteUInt(b, (uint)inp.DimensionToStarboard, 6);
                AisBits.WriteUInt(b, (uint)inp.TypeOfEPFD, 4);
                AisBits.WriteUInt(b, (uint)inp.ETAMonth, 4);
                AisBits.WriteUInt(b, (uint)inp.ETADay, 5);
                AisBits.WriteUInt(b, (uint)inp.ETAHour, 5);
                AisBits.WriteUInt(b, (uint)inp.ETAMinute, 6);
                AisBits.WriteUInt(b, (uint)Math.Round(inp.MaxDraught * 10), 8);
                AisBits.WriteText(b, inp.Destination, 20);
                AisBits.WriteUInt(b, (uint)inp.DTE, 1);
                AisBits.WriteUInt(b, 0, 1); // spare

                var (payload, fill) = AisBits.Encode(b);

                // Split payload into two sentences (Type 5 = 426 bits)
                int half = payload.Length / 2;
                string p1 = payload[..half];
                string p2 = payload[half..];

                res.NmeaSentences.Add(AisBits.BuildNmea(p1, 0, 1, 2, 1, 'A'));
                res.NmeaSentences.Add(AisBits.BuildNmea(p2, fill, 2, 2, 1, 'A'));
                res.EncodedPayload = payload;
                res.BinaryPayload = string.Concat(b);
                res.IsValid = true;
                PopulateDecoded(res, b);
            }
            catch (Exception ex) { res.ErrorMessage = ex.Message; }
            return res;
        }

        public static AisType5Result Parse(string sentence1, string sentence2)
        {
            var res = new AisType5Result();
            try
            {
                sentence1 = sentence1?.Trim() ?? "";
                sentence2 = sentence2?.Trim() ?? "";

                if (string.IsNullOrEmpty(sentence1))
                { res.ErrorMessage = "First sentence is empty."; return res; }

                if (string.IsNullOrEmpty(sentence2))
                { res.ErrorMessage = "Type 5 needs two sentences. Paste both lines in the text box."; return res; }

                var n1 = NmeaParser.Parse(sentence1);
                var n2 = NmeaParser.Parse(sentence2);

                if (!n1.IsValid) { res.ErrorMessage = $"Sentence 1: {n1.ErrorMessage}"; return res; }
                if (!n2.IsValid) { res.ErrorMessage = $"Sentence 2: {n2.ErrorMessage}"; return res; }

                var (p1, _) = NmeaParser.ExtractPayloadAndFill(n1);
                var (p2, fill2) = NmeaParser.ExtractPayloadAndFill(n2);

                if (string.IsNullOrEmpty(p1)) { res.ErrorMessage = "No payload in sentence 1."; return res; }
                if (string.IsNullOrEmpty(p2)) { res.ErrorMessage = "No payload in sentence 2."; return res; }

                // Decode BOTH payloads with fill=0 (never strip mid-message).
                // AIS Type 5 is 426 bits split across two sentences. Fill bits
                // are padding at the armored-ASCII layer only — the real message
                // bits span both sentences, so we decode everything then truncate.
                var bits1 = AisBits.Decode(p1, 0);
                var bits2 = AisBits.Decode(p2, 0);   // always 0 — never strip here
                var bits = new List<int>(bits1);
                bits.AddRange(bits2);

                if (bits.Count < 426)
                { res.ErrorMessage = $"Combined payload too short: {bits.Count} bits (need 426). Make sure both sentences are pasted."; return res; }

                // Discard trailing fill padding, keep exactly 426 message bits
                if (bits.Count > 426)
                    bits.RemoveRange(426, bits.Count - 426);

                int msgType = (int)AisBits.ReadUInt(bits, 0, 6);
                if (msgType != 5)
                { res.ErrorMessage = $"This is a Type {msgType} message, not Type 5."; return res; }

                res.EncodedPayload = p1 + p2;
                res.BinaryPayload = string.Concat(bits);
                res.NmeaSentences.Add(sentence1);
                res.NmeaSentences.Add(sentence2);
                res.IsValid = true;
                PopulateDecoded(res, bits);
            }
            catch (Exception ex) { res.ErrorMessage = $"Parse error: {ex.Message}"; }
            return res;
        }

        static void PopulateDecoded(AisType5Result res, List<int> b)
        {
            res.MessageType = (int)AisBits.ReadUInt(b, 0, 6);
            res.RepeatIndicator = (int)AisBits.ReadUInt(b, 6, 2);
            res.MMSI = (int)AisBits.ReadUInt(b, 8, 30);
            res.AisVersion = (int)AisBits.ReadUInt(b, 38, 2);
            res.IMONumber = (int)AisBits.ReadUInt(b, 40, 30);
            res.CallSign = AisBits.ReadText(b, 70, 7);
            res.VesselName = AisBits.ReadText(b, 112, 20);
            int st = (int)AisBits.ReadUInt(b, 232, 8);
            res.ShipType = st < ShipTypes.Length && !string.IsNullOrEmpty(ShipTypes[st])
                                     ? $"{st} – {ShipTypes[st]}" : st.ToString();
            res.DimensionToBow = (int)AisBits.ReadUInt(b, 240, 9);
            res.DimensionToStern = (int)AisBits.ReadUInt(b, 249, 9);
            res.DimensionToPort = (int)AisBits.ReadUInt(b, 258, 6);
            res.DimensionToStarboard = (int)AisBits.ReadUInt(b, 264, 6);
            int epfd = (int)AisBits.ReadUInt(b, 270, 4);
            res.TypeOfEPFD = epfd < EpfdTypes.Length ? EpfdTypes[epfd] : "Unknown";
            int etaM = (int)AisBits.ReadUInt(b, 274, 4);
            int etaD = (int)AisBits.ReadUInt(b, 278, 5);
            int etaH = (int)AisBits.ReadUInt(b, 283, 5);
            int etaMi = (int)AisBits.ReadUInt(b, 288, 6);
            res.ETA = $"{etaM:D2}/{etaD:D2} {etaH:D2}:{etaMi:D2} UTC";
            res.MaxDraught = AisBits.ReadUInt(b, 294, 8) / 10.0;
            res.Destination = AisBits.ReadText(b, 302, 20);
            res.DTE = (int)AisBits.ReadUInt(b, 422, 1);
        }
    }
}
