using Microsoft.AspNetCore.Mvc;
using NmeaAisParser.Models;
using NmeaAisParser.Services;

namespace NmeaAisParser.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View(new HomeViewModel());

        [HttpPost]
        public IActionResult ParseNmea(HomeViewModel vm)
        {
            vm.ActiveTab = "nmea";
            if (!string.IsNullOrWhiteSpace(vm.NmeaInput))
                vm.NmeaResult = NmeaParser.Parse(vm.NmeaInput.Trim());
            return View("Index", vm);
        }

        [HttpPost]
        public IActionResult GenerateType1(HomeViewModel vm)
        {
            vm.ActiveTab = "type1gen";
            vm.Type1Result = AisType1Service.Generate(vm.Type1Input);
            return View("Index", vm);
        }

        [HttpPost]
        public IActionResult GenerateType5(HomeViewModel vm)
        {
            vm.ActiveTab = "type5gen";
            vm.Type5Result = AisType5Service.Generate(vm.Type5Input);
            return View("Index", vm);
        }

        [HttpPost]
        public IActionResult ParseAis(HomeViewModel vm)
        {
            vm.ActiveTab = "aisparse";

            if (string.IsNullOrWhiteSpace(vm.AisParseInput))
                return View("Index", vm);

            // Split into non-empty trimmed lines
            var lines = vm.AisParseInput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();

            if (lines.Length == 0)
                return View("Index", vm);

            string first = lines[0];

            try
            {
                // Parse the first sentence to extract the payload
                var nmea = NmeaParser.Parse(first);
                if (!nmea.IsValid)
                {
                    vm.AisParseResult = new AisType1Result { ErrorMessage = $"Invalid NMEA: {nmea.ErrorMessage}" };
                    vm.AisParseType = "error";
                    return View("Index", vm);
                }

                // Extract payload using the robust helper
                var (payload, _) = NmeaParser.ExtractPayloadAndFill(nmea);

                if (string.IsNullOrEmpty(payload))
                {
                    vm.AisParseResult = new AisType1Result { ErrorMessage = "No AIS payload found in the sentence." };
                    vm.AisParseType = "error";
                    return View("Index", vm);
                }

                // Peek at message type (first 6 bits)
                var peekBits = AisBits.Decode(payload, 0);
                if (peekBits.Count < 6)
                {
                    vm.AisParseResult = new AisType1Result { ErrorMessage = "Payload is too short to determine message type." };
                    vm.AisParseType = "error";
                    return View("Index", vm);
                }

                int msgType = (int)AisBits.ReadUInt(peekBits, 0, 6);

                if (msgType is 1 or 2 or 3)
                {
                    vm.AisParseResult = AisType1Service.Parse(first);
                    vm.AisParseType = "type1";
                }
                else if (msgType == 5)
                {
                    string second = lines.Length > 1 ? lines[1] : "";
                    vm.AisParseResult = AisType5Service.Parse(first, second);
                    vm.AisParseType = "type5";
                }
                else
                {
                    vm.AisParseResult = new AisType1Result
                    {
                        ErrorMessage = $"AIS message type {msgType} is not supported. This tool handles Type 1, 2, 3, and 5."
                    };
                    vm.AisParseType = "error";
                }
            }
            catch (Exception ex)
            {
                vm.AisParseResult = new AisType1Result { ErrorMessage = $"Unexpected error: {ex.Message}" };
                vm.AisParseType = "error";
            }

            return View("Index", vm);
        }

        // JSON API endpoints
        [HttpPost]
        public IActionResult ApiParseNmea([FromBody] string sentence) =>
            Json(NmeaParser.Parse(sentence));

        [HttpPost]
        public IActionResult ApiGenerateType1([FromBody] AisType1Input input) =>
            Json(AisType1Service.Generate(input));

        [HttpPost]
        public IActionResult ApiGenerateType5([FromBody] AisType5Input input) =>
            Json(AisType5Service.Generate(input));
    }
}
