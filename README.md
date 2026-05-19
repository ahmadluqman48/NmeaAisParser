# NMEA / AIS Workbench – ASP.NET Core 8 MVC

A full-featured NMEA 0183 sentence parser and AIS (Automatic Identification System) encoder/decoder built with **ASP.NET Core 8 MVC**.

---

## Features

| Feature | Details |
|---|---|
| **NMEA Parser** | Parses any NMEA 0183 sentence (GGA, RMC, VTG, GLL, ZDA, VDM, VDO…) with checksum validation |
| **AIS Type 1 Generator** | Encodes a 168-bit Position Report Class A into a valid NMEA VDM sentence |
| **AIS Type 5 Generator** | Encodes a 426-bit Static & Voyage Data message into two NMEA VDM sentences |
| **AIS Parser** | Auto-detects and decodes Type 1/2/3 and Type 5 AIS sentences |
| **Bit-accurate encoding** | Full ITU-R M.1371-5 compliant bit packing/unpacking |
| **Checksum** | NMEA XOR checksum computed and validated on all sentences |

---

## Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Run

```bash
cd NmeaAisParser
dotnet run
```

Then open **http://localhost:5000** in your browser.

---

## Project Structure

```
NmeaAisParser/
├── Controllers/
│   └── HomeController.cs       # All MVC actions + JSON API endpoints
├── Models/
│   └── Models.cs               # All view models and data models
├── Services/
│   └── ParserServices.cs       # NMEA parser, AIS bit encoder, Type1/5 services
├── Views/
│   ├── Home/
│   │   └── Index.cshtml        # Main 4-tab UI
│   └── Shared/
│       └── _Layout.cshtml      # Maritime dark layout
├── wwwroot/
│   ├── css/site.css            # Maritime industrial dark theme
│   └── js/site.js              # Tab switching, copy, sample sentences
└── Program.cs
```

---

## AIS Encoding Details

### Type 1 – Position Report Class A (168 bits)

| Bits | Field |
|---|---|
| 0–5 | Message Type (6 bits) |
| 6–7 | Repeat Indicator (2 bits) |
| 8–37 | MMSI (30 bits) |
| 38–41 | Navigation Status (4 bits) |
| 42–49 | Rate of Turn (8 bits, signed) |
| 50–59 | Speed Over Ground × 10 (10 bits) |
| 60 | Position Accuracy (1 bit) |
| 61–88 | Longitude × 600000 (28 bits, signed) |
| 89–115 | Latitude × 600000 (27 bits, signed) |
| 116–127 | Course Over Ground × 10 (12 bits) |
| 128–136 | True Heading (9 bits) |
| 137–142 | Time Stamp (6 bits) |
| 143–144 | Maneuver Indicator (2 bits) |
| 145–147 | Spare (3 bits) |
| 148 | RAIM Flag (1 bit) |
| 149–167 | Radio Status (19 bits) |

### Type 5 – Static & Voyage Related Data (426 bits, 2 sentences)

| Bits | Field |
|---|---|
| 0–5 | Message Type |
| 8–37 | MMSI |
| 38–39 | AIS Version |
| 40–69 | IMO Number |
| 70–111 | Call Sign (7 × 6-bit ASCII) |
| 112–231 | Vessel Name (20 × 6-bit ASCII) |
| 232–239 | Ship and Cargo Type |
| 240–269 | Dimensions (bow/stern/port/starboard) |
| 270–273 | EPFD Type |
| 274–293 | ETA (month/day/hour/minute) |
| 294–301 | Maximum Draught × 10 |
| 302–421 | Destination (20 × 6-bit ASCII) |
| 422 | DTE |

---

## JSON API Endpoints

```
POST /Home/ApiParseNmea        body: "sentence string"
POST /Home/ApiGenerateType1    body: AisType1Input JSON
POST /Home/ApiGenerateType5    body: AisType5Input JSON
```

---

## Sample NMEA Sentences for Testing

**GGA:**
```
$GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47
```

**RMC:**
```
$GPRMC,092204.999,A,4250.5589,S,14718.5084,E,0.00,89.68,211200,,*25
```

**AIS Type 1:**
```
!AIVDM,1,1,,A,15M67N0P00G?Uf6E`FepT@4n0000,0*73
```

**AIS Type 5 (2 sentences):**
```
!AIVDM,2,1,3,B,55?MbV02<h4eL4LE800l4p4r@Tp0000000000l1@T554400Ht0000000000,0*1B
!AIVDM,2,2,3,B,00000000000,2*25
```
