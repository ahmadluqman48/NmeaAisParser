using Microsoft.AspNetCore.Mvc;
using NmeaAisParser.Models;
using NmeaAisParser.Services;

namespace NmeaAisParser.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View(new HomeViewModel());
        }

        [HttpPost]
        public IActionResult ParseNmea(HomeViewModel vm)
        {
            vm.ActiveTab = "nmea";
            if (!string.IsNullOrWhiteSpace(vm.NmeaInput))
                vm.NmeaResult = NmeaParser.Parse(vm.NmeaInput);
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
            if (!string.IsNullOrWhiteSpace(vm.AisParseInput))
            {
                var lines = vm.AisParseInput.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return View("Index", vm);

                // Detect message type from payload
                string first = lines[0].Trim();
                var nmea = NmeaParser.Parse(first);
                string payload = "";
                if (nmea.Fields.TryGetValue("Field 5", out var p)) payload = p;

                if (payload.Length > 0)
                {
                    string binary = AisBitEncoder.PayloadToBinary(payload);
                    int msgType = (int)AisBitEncoder.ReadUInt(binary, 0, 6);

                    if (msgType is 1 or 2 or 3)
                    {
                        vm.AisParseResult = AisType1Service.Parse(first);
                        vm.AisParseType = "type1";
                    }
                    else if (msgType == 5)
                    {
                        string second = lines.Length > 1 ? lines[1].Trim() : "";
                        vm.AisParseResult = AisType5Service.Parse(first, second);
                        vm.AisParseType = "type5";
                    }
                    else
                    {
                        vm.AisParseType = "unknown";
                    }
                }
            }
            return View("Index", vm);
        }

        // API endpoints for JSON results
        [HttpPost]
        public IActionResult ApiParseNmea([FromBody] string sentence)
        {
            return Json(NmeaParser.Parse(sentence));
        }

        [HttpPost]
        public IActionResult ApiGenerateType1([FromBody] AisType1Input input)
        {
            return Json(AisType1Service.Generate(input));
        }

        [HttpPost]
        public IActionResult ApiGenerateType5([FromBody] AisType5Input input)
        {
            return Json(AisType5Service.Generate(input));
        }
    }
}
