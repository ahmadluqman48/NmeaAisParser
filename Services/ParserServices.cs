using NmeaAisParser.Models;
using System.Text;

namespace NmeaAisParser.Services
{
    public static class NmeaParser
    {
        private static readonly Dictionary<string, string[]> SentenceFieldNames = new()
        {
            ["GGA"] = new[] { "UTC Time", "Latitude", "N/S", "Longitude", "E/W", "Fix Quality", "Satellites", "HDOP", "Altitude", "Alt Unit", "Geoid Sep", "Geoid Unit", "Age DGPS", "DGPS Station" },
            ["RMC"] = new[] { "UTC Time", "Status", "Latitude", "N/S", "Longitude", "E/W", "Speed (knots)", "Track Angle", "Date", "Magnetic Var", "Mag Var Dir", "Mode" },
            ["GLL"] = new[] { "Latitude", "N/S", "Longitude", "E/W", "UTC Time", "Status", "Mode" },
            ["GSA"] = new[] { "Mode 1", "Mode 2", "SV1","SV2","SV3","SV4","SV5","SV6","SV7","SV8","SV9","SV10","SV11","SV12", "PDOP", "HDOP", "VDOP" },
            ["VTG"] = new[] { "Track (True)", "T", "Track (Mag)", "M", "Speed (knots)", "N", "Speed (km/h)", "K", "Mode" },
            ["ZDA"] = new[] { "UTC Time", "Day", "Month", "Year", "Local Zone Hours", "Local Zone Minutes" },
            ["VDM"] = new[] { "Count", "Number", "Seq ID", "Channel", "Payload", "Fill Bits" },
            ["VDO"] = new[] { "Count", "Number", "Seq ID", "Channel", "Payload", "Fill Bits" },
        };

        public static NmeaParseResult Parse(string raw)
        {
            var result = new NmeaParseResult { RawSentence = raw };
            try
            {
                raw = raw.Trim();
                if (!raw.StartsWith('$') && !raw.StartsWith('!'))
                {
                    result.ErrorMessage = "Sentence must start with '$' or '!'";
                    return result;
                }

                // Extract and validate checksum
                int asterisk = raw.IndexOf('*');
                if (asterisk >= 0 && asterisk < raw.Length - 1)
                {
                    string providedCs = raw[(asterisk + 1)..].Trim().ToUpper();
                    string computed = ComputeChecksum(raw[1..asterisk]);
                    result.Checksum = computed;
                    result.ChecksumValid = providedCs == computed;
                    raw = raw[..asterisk];
                }
                else
                {
                    result.Checksum = ComputeChecksum(raw[1..]);
                    result.ChecksumValid = false;
                }

                string body = raw[1..]; // strip $ or !
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

                // Map fields
                if (SentenceFieldNames.TryGetValue(result.MessageType, out var fieldNames))
                {
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string name = (i - 1) < fieldNames.Length ? fieldNames[i - 1] : $"Field {i}";
                        result.Fields[name] = parts[i];
                    }
                }
                else
                {
                    for (int i = 1; i < parts.Length; i++)
                        result.Fields[$"Field {i}"] = parts[i];
                }

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }
            return result;
        }

        public static string ComputeChecksum(string data)
        {
            byte cs = 0;
            foreach (char c in data) cs ^= (byte)c;
            return cs.ToString("X2");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS bit-level encoder/decoder helpers
    // ─────────────────────────────────────────────────────────────
    public static class AisBitEncoder
    {
        // Encode integer into n bits (MSB first)
        public static void WriteUInt(StringBuilder sb, uint value, int bits)
        {
            for (int i = bits - 1; i >= 0; i--)
                sb.Append((value >> i) & 1);
        }

        public static void WriteInt(StringBuilder sb, int value, int bits)
        {
            // Two's complement
            uint u = (uint)(value & ((1 << bits) - 1));
            WriteUInt(sb, u, bits);
        }

        // AIS 6-bit ASCII text (padded with spaces)
        public static void WriteText(StringBuilder sb, string text, int charCount)
        {
            text = text.ToUpper().PadRight(charCount).Substring(0, charCount);
            foreach (char c in text)
            {
                int v = c switch
                {
                    ' ' => 32,
                    '@' => 0,
                    _ when c >= 'A' && c <= 'Z' => c - 'A' + 1,
                    _ when c >= '0' && c <= '9' => c - '0' + 48,
                    _ => 32
                };
                WriteUInt(sb, (uint)v, 6);
            }
        }

        // Convert binary string to AIS armored ASCII payload
        public static (string payload, int fillBits) BinaryToPayload(string bits)
        {
            int fillBits = (6 - bits.Length % 6) % 6;
            bits = bits.PadRight(bits.Length + fillBits, '0');
            var sb = new StringBuilder();
            for (int i = 0; i < bits.Length; i += 6)
            {
                int v = Convert.ToInt32(bits.Substring(i, 6), 2);
                v += 48;
                if (v > 87) v += 8;
                sb.Append((char)v);
            }
            return (sb.ToString(), fillBits);
        }

        // Decode AIS armored ASCII back to binary
        public static string PayloadToBinary(string payload)
        {
            var sb = new StringBuilder();
            foreach (char c in payload)
            {
                int v = c - 48;
                if (v > 39) v -= 8;
                for (int i = 5; i >= 0; i--)
                    sb.Append((v >> i) & 1);
            }
            return sb.ToString();
        }

        public static uint ReadUInt(string bits, int start, int length)
        {
            uint v = 0;
            for (int i = 0; i < length; i++)
                v = (v << 1) | (uint)(bits[start + i] - '0');
            return v;
        }

        public static int ReadInt(string bits, int start, int length)
        {
            uint u = ReadUInt(bits, start, length);
            if ((u & (1u << (length - 1))) != 0)
                u |= ~((1u << length) - 1u); // sign extend
            return (int)u;
        }

        public static string ReadText(string bits, int start, int charCount)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < charCount; i++)
            {
                uint v = ReadUInt(bits, start + i * 6, 6);
                char c = (char)(v switch
                {
                    0 => '@',
                    >= 1 and <= 26 => v + 'A' - 1,
                    >= 48 and <= 57 => v + '0' - 48,
                    32 => ' ',
                    _ => ' '
                });
                sb.Append(c);
            }
            return sb.ToString().TrimEnd();
        }

        public static string BuildNmea(string talker, string payload, int fillBits, int part, int total, int seqId, char channel)
        {
            string body = $"{talker},{total},{part},{(seqId > 0 ? seqId.ToString() : "")},{channel},{payload},{fillBits}";
            string cs = NmeaParser.ComputeChecksum(body);
            return $"!{body}*{cs}";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS Type 1 service
    // ─────────────────────────────────────────────────────────────
    public static class AisType1Service
    {
        private static readonly string[] NavStatusNames = {
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
                var bits = new StringBuilder();

                AisBitEncoder.WriteUInt(bits, (uint)inp.MessageType, 6);
                AisBitEncoder.WriteUInt(bits, (uint)inp.RepeatIndicator, 2);
                AisBitEncoder.WriteUInt(bits, (uint)inp.MMSI, 30);
                AisBitEncoder.WriteUInt(bits, (uint)inp.NavigationStatus, 4);
                AisBitEncoder.WriteInt(bits, inp.RateOfTurn, 8);
                AisBitEncoder.WriteUInt(bits, (uint)Math.Round(inp.SpeedOverGround * 10), 10);
                AisBitEncoder.WriteUInt(bits, (uint)inp.PositionAccuracy, 1);
                AisBitEncoder.WriteInt(bits, (int)Math.Round(inp.Longitude * 600000), 28);
                AisBitEncoder.WriteInt(bits, (int)Math.Round(inp.Latitude * 600000), 27);
                AisBitEncoder.WriteUInt(bits, (uint)Math.Round(inp.CourseOverGround * 10), 12);
                AisBitEncoder.WriteUInt(bits, (uint)inp.TrueHeading, 9);
                AisBitEncoder.WriteUInt(bits, (uint)inp.TimeStamp, 6);
                AisBitEncoder.WriteUInt(bits, (uint)inp.ManeuverIndicator, 2);
                AisBitEncoder.WriteUInt(bits, 0, 3); // spare
                AisBitEncoder.WriteUInt(bits, (uint)inp.RAIMFlag, 1);
                AisBitEncoder.WriteUInt(bits, 0, 19); // radio status

                string binary = bits.ToString();
                var (payload, fillBits) = AisBitEncoder.BinaryToPayload(binary);

                res.BinaryPayload = binary;
                res.EncodedPayload = payload;
                res.NmeaSentence = AisBitEncoder.BuildNmea("AIVDM", payload, fillBits, 1, 1, 0, 'A');
                res.IsValid = true;
                Decode(res, binary, inp);
            }
            catch (Exception ex) { res.ErrorMessage = ex.Message; }
            return res;
        }

        public static AisType1Result Parse(string sentence)
        {
            var res = new AisType1Result();
            try
            {
                var nmea = NmeaParser.Parse(sentence);
                if (!nmea.IsValid) { res.ErrorMessage = nmea.ErrorMessage; return res; }
                string payload = nmea.Fields.ContainsKey("Payload") ? nmea.Fields["Payload"] : nmea.Fields["Field 5"];
                string binary = AisBitEncoder.PayloadToBinary(payload);
                if (binary.Length < 168) { res.ErrorMessage = "Payload too short"; return res; }
                res.BinaryPayload = binary;
                res.EncodedPayload = payload;
                res.NmeaSentence = sentence;
                Decode(res, binary, null);
                res.IsValid = true;
            }
            catch (Exception ex) { res.ErrorMessage = ex.Message; }
            return res;
        }

        private static void Decode(AisType1Result res, string bits, AisType1Input? inp)
        {
            res.MessageType = (int)AisBitEncoder.ReadUInt(bits, 0, 6);
            res.RepeatIndicator = (int)AisBitEncoder.ReadUInt(bits, 6, 2);
            res.MMSI = (int)AisBitEncoder.ReadUInt(bits, 8, 30);
            int navStatus = (int)AisBitEncoder.ReadUInt(bits, 38, 4);
            res.NavigationStatus = navStatus < NavStatusNames.Length ? NavStatusNames[navStatus] : "Unknown";
            res.RateOfTurn = AisBitEncoder.ReadInt(bits, 42, 8);
            res.SpeedOverGround = AisBitEncoder.ReadUInt(bits, 50, 10) / 10.0;
            res.PositionAccuracy = (int)AisBitEncoder.ReadUInt(bits, 60, 1);
            res.Longitude = AisBitEncoder.ReadInt(bits, 61, 28) / 600000.0;
            res.Latitude = AisBitEncoder.ReadInt(bits, 89, 27) / 600000.0;
            res.CourseOverGround = AisBitEncoder.ReadUInt(bits, 116, 12) / 10.0;
            res.TrueHeading = (int)AisBitEncoder.ReadUInt(bits, 128, 9);
            res.TimeStamp = (int)AisBitEncoder.ReadUInt(bits, 137, 6);
            res.ManeuverIndicator = (int)AisBitEncoder.ReadUInt(bits, 143, 2);
            res.RAIMFlag = (int)AisBitEncoder.ReadUInt(bits, 148, 1);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  AIS Type 5 service
    // ─────────────────────────────────────────────────────────────
    public static class AisType5Service
    {
        private static readonly string[] ShipTypeNames = {
            "Not available","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved",
            "Wing in ground (WIG)","WIG Hazard A","WIG Hazard B","WIG Hazard C","WIG Hazard D","WIG Reserved","WIG Reserved","WIG Reserved","WIG Reserved","WIG Reserved",
            "Fishing","Towing","Towing > 200m","Dredging","Diving","Military","Sailing","Pleasure Craft","Reserved","Reserved",
            "HSC","HSC Hazard A","HSC Hazard B","HSC Hazard C","HSC Hazard D","HSC Reserved","HSC Reserved","HSC Reserved","HSC Reserved","HSC No info",
            "Pilot Vessel","SAR","Tug","Port Tender","Anti-pollution","Law Enforcement","Reserved","Reserved","Medical","Ship according RR Res",
            "Passenger","Passenger Hazard A","Passenger Hazard B","Passenger Hazard C","Passenger Hazard D","Passenger Reserved","Passenger Reserved","Passenger Reserved","Passenger Reserved","Passenger No info",
            "Cargo","Cargo Hazard A","Cargo Hazard B","Cargo Hazard C","Cargo Hazard D","Cargo Reserved","Cargo Reserved","Cargo Reserved","Cargo Reserved","Cargo No info",
            "Tanker","Tanker Hazard A","Tanker Hazard B","Tanker Hazard C","Tanker Hazard D","Tanker Reserved","Tanker Reserved","Tanker Reserved","Tanker Reserved","Tanker No info",
            "Other","Other Hazard A","Other Hazard B","Other Hazard C","Other Hazard D","Other Reserved","Other Reserved","Other Reserved","Other Reserved","Other No info"
        };

        private static readonly string[] EpfdNames = {
            "Undefined","GPS","GLONASS","Combined GPS/GLONASS","Loran-C","Chayka","Integrated Nav System","Surveyed","Galileo"
        };

        public static AisType5Result Generate(AisType5Input inp)
        {
            var res = new AisType5Result();
            try
            {
                var bits = new StringBuilder();
                AisBitEncoder.WriteUInt(bits, (uint)inp.MessageType, 6);
                AisBitEncoder.WriteUInt(bits, (uint)inp.RepeatIndicator, 2);
                AisBitEncoder.WriteUInt(bits, (uint)inp.MMSI, 30);
                AisBitEncoder.WriteUInt(bits, (uint)inp.AisVersion, 2);
                AisBitEncoder.WriteUInt(bits, (uint)inp.IMONumber, 30);
                AisBitEncoder.WriteText(bits, inp.CallSign, 7);
                AisBitEncoder.WriteText(bits, inp.VesselName, 20);
                AisBitEncoder.WriteUInt(bits, (uint)inp.ShipType, 8);
                AisBitEncoder.WriteUInt(bits, (uint)inp.DimensionToBow, 9);
                AisBitEncoder.WriteUInt(bits, (uint)inp.DimensionToStern, 9);
                AisBitEncoder.WriteUInt(bits, (uint)inp.DimensionToPort, 6);
                AisBitEncoder.WriteUInt(bits, (uint)inp.DimensionToStarboard, 6);
                AisBitEncoder.WriteUInt(bits, (uint)inp.TypeOfEPFD, 4);
                AisBitEncoder.WriteUInt(bits, (uint)inp.ETAMonth, 4);
                AisBitEncoder.WriteUInt(bits, (uint)inp.ETADay, 5);
                AisBitEncoder.WriteUInt(bits, (uint)inp.ETAHour, 5);
                AisBitEncoder.WriteUInt(bits, (uint)inp.ETAMinute, 6);
                AisBitEncoder.WriteUInt(bits, (uint)Math.Round(inp.MaxDraught * 10), 8);
                AisBitEncoder.WriteText(bits, inp.Destination, 20);
                AisBitEncoder.WriteUInt(bits, (uint)inp.DTE, 1);
                AisBitEncoder.WriteUInt(bits, 0, 1); // spare

                string binary = bits.ToString(); // 426 bits
                var (payload, fillBits) = AisBitEncoder.BinaryToPayload(binary);

                // Type 5 is split into 2 NMEA sentences (max 56 chars payload each)
                int half = payload.Length / 2;
                string p1 = payload[..half];
                string p2 = payload[half..];

                res.BinaryPayload = binary;
                res.EncodedPayload = payload;
                res.NmeaSentences.Add(AisBitEncoder.BuildNmea("AIVDM", p1, 0, 1, 2, 1, 'A'));
                res.NmeaSentences.Add(AisBitEncoder.BuildNmea("AIVDM", p2, fillBits, 2, 2, 1, 'A'));
                res.IsValid = true;
                Decode(res, binary);
            }
            catch (Exception ex) { res.ErrorMessage = ex.Message; }
            return res;
        }

        public static AisType5Result Parse(string sentence1, string sentence2)
        {
            var res = new AisType5Result();
            try
            {
                var n1 = NmeaParser.Parse(sentence1);
                var n2 = NmeaParser.Parse(sentence2);
                string p1 = n1.Fields.ContainsKey("Payload") ? n1.Fields["Payload"] : n1.Fields["Field 5"];
                string p2 = n2.Fields.ContainsKey("Payload") ? n2.Fields["Payload"] : n2.Fields["Field 5"];
                string binary = AisBitEncoder.PayloadToBinary(p1 + p2);
                if (binary.Length < 426) { res.ErrorMessage = $"Combined payload too short ({binary.Length} bits, need 426)"; return res; }
                res.BinaryPayload = binary;
                res.EncodedPayload = p1 + p2;
                res.NmeaSentences.Add(sentence1);
                res.NmeaSentences.Add(sentence2);
                Decode(res, binary);
                res.IsValid = true;
            }
            catch (Exception ex) { res.ErrorMessage = ex.Message; }
            return res;
        }

        private static void Decode(AisType5Result res, string bits)
        {
            res.MessageType = (int)AisBitEncoder.ReadUInt(bits, 0, 6);
            res.RepeatIndicator = (int)AisBitEncoder.ReadUInt(bits, 6, 2);
            res.MMSI = (int)AisBitEncoder.ReadUInt(bits, 8, 30);
            res.AisVersion = (int)AisBitEncoder.ReadUInt(bits, 38, 2);
            res.IMONumber = (int)AisBitEncoder.ReadUInt(bits, 40, 30);
            res.CallSign = AisBitEncoder.ReadText(bits, 70, 7);
            res.VesselName = AisBitEncoder.ReadText(bits, 112, 20);
            int shipType = (int)AisBitEncoder.ReadUInt(bits, 232, 8);
            res.ShipType = shipType < ShipTypeNames.Length ? $"{shipType} – {ShipTypeNames[shipType]}" : shipType.ToString();
            res.DimensionToBow = (int)AisBitEncoder.ReadUInt(bits, 240, 9);
            res.DimensionToStern = (int)AisBitEncoder.ReadUInt(bits, 249, 9);
            res.DimensionToPort = (int)AisBitEncoder.ReadUInt(bits, 258, 6);
            res.DimensionToStarboard = (int)AisBitEncoder.ReadUInt(bits, 264, 6);
            int epfd = (int)AisBitEncoder.ReadUInt(bits, 270, 4);
            res.TypeOfEPFD = epfd < EpfdNames.Length ? EpfdNames[epfd] : "Unknown";
            int etaM = (int)AisBitEncoder.ReadUInt(bits, 274, 4);
            int etaD = (int)AisBitEncoder.ReadUInt(bits, 278, 5);
            int etaH = (int)AisBitEncoder.ReadUInt(bits, 283, 5);
            int etaMi = (int)AisBitEncoder.ReadUInt(bits, 288, 6);
            res.ETA = $"{etaM:D2}/{etaD:D2} {etaH:D2}:{etaMi:D2} UTC";
            res.MaxDraught = AisBitEncoder.ReadUInt(bits, 294, 8) / 10.0;
            res.Destination = AisBitEncoder.ReadText(bits, 302, 20);
            res.DTE = (int)AisBitEncoder.ReadUInt(bits, 422, 1);
        }
    }
}
