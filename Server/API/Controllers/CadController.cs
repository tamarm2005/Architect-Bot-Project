using BL.Algorithms;
using CadApplication.Algorithms;
using DAL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Models;
using netDxf;
using netDxf.Entities;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CadProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CadController : ControllerBase
    {
        // הקשר לבסיס הנתונים
        private readonly ProjectContext _context;

        // קונסטרקטור שמקבל את ההקשר של בסיס הנתונים
        public CadController(ProjectContext context)
        {
            _context = context;
        }

        // מקבלת קובץ שרטוט אדריכלי, מפרקת אותו לגורמים, מזהה חדרים ורהיטים, שומרת את הדירה בבסיס הנתונים, ובסוף מייצרת אוטומטית תוכנית גג חדשה ומחזירה אותה למשתמש.
        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadDxf(IFormFile file, [FromForm] string roofType = "parapet")
        {
            if (file == null || file.Length == 0) return BadRequest("לא נבחר קובץ.");
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dxf");

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create)) { await file.CopyToAsync(stream); }
                DxfDocument dxf = DxfDocument.Load(tempPath);
                if (dxf == null) return BadRequest("הקובץ לא נטען.");

                var defaultMaterial = await _context.Materials.FirstOrDefaultAsync();
                if (defaultMaterial == null)
                {
                    defaultMaterial = new Material { MaterialName = "Standard Concrete" };
                    _context.Materials.Add(defaultMaterial);
                    await _context.SaveChangesAsync();
                }

                var newApartment = new Apartment
                {
                    Name = file.FileName,
                    Rooms = new List<Room>()
                };

                var defaultRoom = new Room
                {
                    Name = "כללי (מחושב מהשרטוט)",
                    Area = 0,
                    Perimeter = 0,
                    furniture = new List<Furniture>(),
                    openings = new List<Opening>(),
                    walls = new List<Wall>()
                };

                if (dxf.Entities.Lines.Any())
                {
                    defaultRoom.walls = dxf.Entities.Lines.Select(l => new Wall
                    {
                        StartPoint = new Models.Point3D(0, l.StartPoint.X, l.StartPoint.Y, 0 ),
                        EndPoint = new Models.Point3D(0, l.EndPoint.X, l.EndPoint.Y, 0 ),
                        WallType = l.Layer.Name ?? "Exterior Wall",
                        Thickness = 20,
                        Material = defaultMaterial
                    }).ToList();
                }

                foreach (var insert in dxf.Entities.Inserts)
                {
                    string layer = ((EntityObject)insert).Layer.Name;
                    string blockName = insert.Block.Name;
                    bool isOpening = layer.Contains("דלט") || layer.Contains("חלון") || layer.Contains("door") || layer.Contains("window");

                    if (isOpening)
                    {
                        defaultRoom.openings.Add(new Opening
                        {
                            OpenName = blockName,
                            Type = layer.Contains("דלט") ? "Door" : "Window",
                            Width = 90,
                            Height = 210,
                            BottomHeight = layer.Contains("חלון") ? 100 : 0,
                            StartPoint = new Models.Point3D(0, insert.Position.X, insert.Position.Y, 0 ),
                            EndPoint = new Models.Point3D(0, insert.Position.X + 10, insert.Position.Y, 0 ),
                            Materials = new List<Material> { defaultMaterial }
                        });
                    }
                    else
                    {
                        defaultRoom.furniture.Add(new Furniture
                        {
                            ItemName = blockName,
                            Location = new Models.Point3D(0, insert.Position.X, insert.Position.Y, 0 ),
                            Width = 60,
                            Height = 75,
                            Length = 120,
                            Materials = defaultMaterial
                        });
                    }
                }

                var geometryService = new GeometryService();
                var graph = geometryService.BuildGraph(defaultRoom.walls);
                var roomCycles = geometryService.FindRooms(graph);

                newApartment.Rooms = new List<Room>();

                foreach (var cycle in roomCycles)
                {
                    double rawArea = geometryService.CalculateArea(cycle);
                    double roomArea = rawArea / 1.0;
                    if (roomArea < 0.1) roomArea = rawArea;
                    if (roomArea < 0.01) continue;

                    var realRoom = new Room
                    {
                        Name = "חדר מזוהה",
                        Area = roomArea,
                        Perimeter = geometryService.CalculatePerimeter(cycle) / 1.0,
                        Vertices = cycle,
                        furniture = new List<Furniture>(),
                        openings = new List<Opening>(),
                        walls = defaultRoom.walls
                    };

                    var furnitureInRoom = defaultRoom.furniture
                    .Where(f => geometryService.IsPointInPolygon(f.Location, cycle))
                    .ToList();

                    realRoom.furniture = furnitureInRoom; // שיוך!

                    newApartment.Rooms.Add(realRoom);
                }

                _context.Apartments.Add(newApartment);
                await _context.SaveChangesAsync();

                List<netDxf.Entities.Line> actualDxfLines = new List<netDxf.Entities.Line>();

                foreach (var l in dxf.Entities.Lines)
                {
                    actualDxfLines.Add(l);
                }

                foreach (var poly in dxf.Entities.Polylines2D)
                {
                    var vertices = poly.Vertexes;
                    for (int i = 0; i < vertices.Count - 1; i++)
                    {
                        actualDxfLines.Add(new netDxf.Entities.Line(
                            new netDxf.Vector2(vertices[i].Position.X, vertices[i].Position.Y),
                            new netDxf.Vector2(vertices[i + 1].Position.X, vertices[i + 1].Position.Y)
                        )
                        { Layer = poly.Layer });
                    }
                    if (poly.IsClosed && vertices.Count > 2)
                    {
                        actualDxfLines.Add(new netDxf.Entities.Line(
                            new netDxf.Vector2(vertices[vertices.Count - 1].Position.X, vertices[vertices.Count - 1].Position.Y),
                            new netDxf.Vector2(vertices[0].Position.X, vertices[0].Position.Y)
                        )
                        { Layer = poly.Layer });
                    }
                }

                var filteredLines = actualDxfLines
                    .Where(l => l.Layer.Name.ToUpper().Contains("WALL") ||
                                l.Layer.Name.Contains("קיר") ||
                                l.Layer.Name.ToUpper().Contains("חוץ") ||
                                l.Layer.Name.ToUpper().Contains("EXT"))
                    .ToList();

                if (!filteredLines.Any())
                {
                    filteredLines = actualDxfLines;
                }

                if (!filteredLines.Any())
                    return BadRequest("לא נמצאו קווים או פולילינות תקינים בקובץ ה-DXF לצורך יצירת הגג.");

                var roofService = new RoofService();
                var spec = new RoofService.RoofSpecification
                {
                    RoofType = roofType,
                    SlopePercent = (roofType.ToLower() == "parapet" || roofType.ToLower() == "flat") ? 1.5 : 35.0,
                    WallThickness = 20.0
                };

                string generatedFileName = roofService.GenerateRoofPlanFromWalls(filteredLines, spec);

                if (generatedFileName.StartsWith("Error"))
                {
                    return BadRequest($"שגיאה בהפקת הגג: {generatedFileName}");
                }

                if (System.IO.File.Exists(generatedFileName))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(generatedFileName);
                    System.IO.File.Delete(generatedFileName);
                    return File(fileBytes, "application/octet-stream", $"Roof_{roofType}_{file.FileName}");
                }

                return BadRequest("קובץ הגג נוצר אך לא נמצא בשרת.");
            }
            catch (Exception ex)
            {
                return BadRequest($"שגיאה: {ex.Message}");
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
        }

        // מאפשר למשתמש להוריד את הדירה כקובץ טקסט עם כל המידע שלה
        [HttpGet("download-report/{apartmentId}")]
        public async Task<IActionResult> DownloadApartmentReport(int apartmentId)
        {
            var apartment = await _context.Apartments
                .Include(a => a.Rooms)
                    .ThenInclude(r => r.furniture)
                .FirstOrDefaultAsync(a => a.Id == apartmentId);

            if (apartment == null) return NotFound("הדירה לא נמצאה.");

            string reportContent = apartment.DataForDownload();
            var fileBytes = System.Text.Encoding.UTF8.GetBytes(reportContent);

            return File(fileBytes, "text/plain", $"Apartment_Report_{apartmentId}.txt");
        }

        // מאפשר למשתמש להעלות קובץ DXF של מגרש, מזהה את המגרש ומחזירה את המידע למשתמש
        [HttpPost("upload-plot")]
        public async Task<IActionResult> UploadPlot(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("לא נבחר קובץ.");
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".dxf");

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create)) { await file.CopyToAsync(stream); }
                DxfDocument dxf = DxfDocument.Load(tempPath);
                if (dxf == null) return BadRequest("הקובץ פגום או לא נתמך.");

                var geometryService = new GeometryService();

                var candidates = dxf.Entities.Polylines2D
                    .Where(p => p.IsClosed && p.Vertexes.Count > 2)
                    .Select((p, index) => new
                    {
                        Id = index,
                        LayerName = p.Layer?.Name ?? "No Layer",
                        Area = Math.Round(geometryService.CalculateArea(p.Vertexes.Select(v => new Models.Point3D(0, v.Position.X, v.Position.Y, 0 )).ToList()), 2),
                        Points = p.Vertexes.Select(v => new Models.Point3D(0, v.Position.X, v.Position.Y, 0 )).ToList(),
                        Centroid = geometryService.GetCentroid(p.Vertexes.Select(v => new Models.Point3D(0, v.Position.X, v.Position.Y, 0 )).ToList())
                    })
                    .OrderByDescending(c => c.Area)
                    .Take(1000)
                    .ToList();

                var boundaryLayerNames = new[] { "BOUNDARY", "PROPERTY", "PLOT", "MIGRASH", "GADER" };
                var autoMatch = candidates.FirstOrDefault(c => boundaryLayerNames.Any(name => c.LayerName.ToUpper().Contains(name)));

                if (autoMatch == null)
                {
                    return Ok(new
                    {
                        message = "לא זוהה מגרש אוטומטית. אנא בחר מהרשימה:",
                        options = candidates
                    });
                }

                var newPlot = new Plot
                {
                    Name = file.FileName,
                    TotalArea = autoMatch.Area,
                    CreatedAt = DateTime.Now
                };

                _context.Plots.Add(newPlot);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "המגרש זוהה בהצלחה",
                    plotId = newPlot.PlotId,
                    area = autoMatch.Area,
                    points = autoMatch.Points
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"שגיאה בעיבוד הקובץ: {ex.Message}");
            }
            finally
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
            }
        }

        // מאשר את המגרש שנבחר על ידי המשתמש ושומר אותו בבסיס הנתונים
        [HttpPost("confirm-plot")]
        public async Task<IActionResult> ConfirmPlot([FromBody] PlotSelection selection)
        {
            var plot = new Plot
            {
                Name = "מגרש נבחר",
                TotalArea = selection.Area
            };

            _context.Plots.Add(plot);
            await _context.SaveChangesAsync();

            return Ok(new { message = "המגרש אושר בהצלחה!", plotId = plot.PlotId });
        }

        // מודל לקבלת המגרש שנבחר על ידי המשתמש
        public class PlotSelection
        {
            public int SelectedId { get; set; }
            public double Area { get; set; }
        }

        // מאפשר למשתמש להזין מידות של גג וליצור תוכנית גג חדשה
        [HttpPost("generate-roof")]
        public IActionResult GenerateRoof([FromQuery] double length, [FromQuery] double width, [FromQuery] string roofType)
        {
            try
            {
                var roofService = new BL.Algorithms.RoofService();

                var spec = new RoofService.RoofSpecification
                {
                    RoofType = roofType,
                    SlopePercent = (roofType.ToLower() == "parapet" || roofType.ToLower() == "flat") ? 1.5 : 35.0,
                    WallThickness = 20.0
                };

                string generatedFileName = roofService.GenerateRoofPlan((int)length, (int)width, spec);

                if (generatedFileName.StartsWith("Error:")) return BadRequest(generatedFileName);
                if (!System.IO.File.Exists(generatedFileName)) return NotFound("הקובץ לא נמצא בשרת.");

                var fileBytes = System.IO.File.ReadAllBytes(generatedFileName);
                try { System.IO.File.Delete(generatedFileName); } catch { }

                return File(fileBytes, "application/octet-stream", Path.GetFileName(generatedFileName));
            }
            catch (Exception ex)
            {
                return BadRequest($"שגיאה ביצירת הגג: {ex.Message}");
            }
        }

        // מודל לקבלת בקשת הצ'אט מהקליינט (React)
        public class ChatRequest
        {
            [JsonPropertyName("text")]
            public string Text { get; set; }
        }

        // מודל לקבלת התגובה מהשרת של הבוט (Python)
        public class NlpResponseWrapper
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("data")]
            public ArchitecturalData Data { get; set; }

            [JsonPropertyName("error")]
            public string Error { get; set; }
        }

        // מודל שמייצג את הנתונים האדריכליים שהבוט מחזיר
        public class ArchitecturalData
        {
            [JsonPropertyName("action")]
            public string Action { get; set; }

            [JsonPropertyName("roomType")]
            public string RoomType { get; set; }

            [JsonPropertyName("width")]
            public double Width { get; set; }

            [JsonPropertyName("length")]
            public double Length { get; set; }
        }

        // מודל שמייצג את התגובה מהשרת של הבוט (Python)
        public class ChatResponse { public string Reply { get; set; } }

        // מאפשר למשתמש לשאול את הבוט שאלות בצ'אט ולקבל תשובות
        [HttpPost("ask-bot")]
        public async Task<IActionResult> AskBot([FromBody] ChatRequest request)
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
                    var json = JsonSerializer.Serialize(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var pythonUrl = "http://127.0.0.1:5000/chat";
                    var response = await client.PostAsync(pythonUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
                        return Ok(new { reply = result?.Reply });
                    }

                    return BadRequest("שרת הבינה המלאכותית לא זמין כרגע.");
                }
                catch (Exception ex)
                {
                    return BadRequest($"שגיאה בתקשורת עם הבוט: {ex.Message}");
                }
            }
        }

        // מאפשר למשתמש להריץ את אלגוריתם מיקום הרהיטים על חדר מסוים ולשמור את המיקומים בבסיס הנתונים
        [HttpPost("arrange-room/{roomId}")]
        public IActionResult ArrangeRoom(int roomId)
        {
            var room = _context.Rooms.Include(r => r.furniture).FirstOrDefault(r => r.ID == roomId);

            var arranger = new FurniturePlacementAlgorithm();
            arranger.RunPlacement(room);

            _context.SaveChanges();
            return Ok(room);
        }

        // מאפשר למשתמש לייצא את החדר כקובץ DXF עם הרהיטים במיקומים שלהם
        [HttpPost("export-room-dxf/{roomId}")]
        public IActionResult ExportRoomToDxf(int roomId)
        {
            var room = _context.Rooms.Include(r => r.furniture).FirstOrDefault(r => r.ID == roomId);
            if (room == null) return NotFound();

            // יצירת קובץ DXF חדש
            var doc = new netDxf.DxfDocument();

            // בדיקת אמת
            if (room.furniture == null || room.furniture.Count == 0)
                return BadRequest("החדר נמצא אבל רשימת הרהיטים ריקה - האלגוריתם לא עדכן את ה-DB!");

            // הוספת הרהיטים ל-DXF
            foreach (var f in room.furniture)
            {
                // יוצרים מלבן לכל רהיט
                var p1 = new netDxf.Vector2(f.Location.X, f.Location.Y);
                var p2 = new netDxf.Vector2(f.Location.X + f.Width, f.Location.Y);
                var p3 = new netDxf.Vector2(f.Location.X + f.Width, f.Location.Y + f.Length);
                var p4 = new netDxf.Vector2(f.Location.X, f.Location.Y + f.Length);

                var poly = new netDxf.Entities.Polyline2D(new List<netDxf.Vector2> { p1, p2, p3, p4, p1 });
                doc.Entities.Add(poly);
            }

            string fileName = $"Room_{roomId}_{Guid.NewGuid()}.dxf";
            doc.Save(fileName);

            // הורדת הקובץ בדיוק כמו שעשית בגג
            var fileBytes = System.IO.File.ReadAllBytes(fileName);
            System.IO.File.Delete(fileName);
            return File(fileBytes, "application/dxf", fileName);
        }

        // מכניס אופסט למגרש כדי שיהיה קו חיצוני למבנה
        [HttpPost("generate-envelope")]
        public IActionResult GenerateEnvelope([FromBody] List<Models.Point3D> selectedPlotPoints)
        {
            if (selectedPlotPoints == null || selectedPlotPoints.Count < 3)
                return BadRequest("יש לבחור מגרש עם לפחות 3 קודקודים.");

            var layoutService = new BL.Algorithms.LayoutGeneratorService();

            // יוצרים מעטפת פנימית במרחק של 200 יחידות (מטרים/סנטימטרים לפי קנה המידה בשרטוט)
            var buildingEnvelope = layoutService.GenerateBuildingFootprint(selectedPlotPoints, 200.0);

            return Ok(new
            {
                message = "מעטפת המבנה נוצרה בהצלחה",
                envelopePoints = buildingEnvelope
            });
        }

        // מאפשר למשתמש להזין את המגרש ואת החדרים שהוא רוצה, ומחזיר את התוכנית הסופית עם מיקום החדרים והריהוט
        private readonly LayoutGeneratorService _layoutService = new LayoutGeneratorService();
        private readonly FurniturePlacementAlgorithm _placementAlgorithm = new FurniturePlacementAlgorithm();
        // כאן אנחנו מגדירים שהמודל ההיברידי הוא הבסיס המרכזי למערכת
        private readonly BL.Algorithms.HybridArchitectureModel _hybridModel = new BL.Algorithms.HybridArchitectureModel();

        // מודל לקבלת הנתונים מהקליינט (React)
        public class LayoutRequest
        {
            internal List<ArchitecturalRoom> Rooms;

            public List<Point3D> PlotVertices { get; set; } // קודקודי המגרש המקורי
            public List<ArchitecturalRoom> RequestedRooms { get; set; } // החדרים שהמשתמש ביקש בצ'אט
        }

        // 🌟 DTO חדש: מייצג תוכנית שכבר חושבה ומאושרת - בלי צורך בחישוב מחדש
        public class ConfirmedFurnitureDto
        {
            public string Name { get; set; }
            public double Width { get; set; }
            public double Length { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Rotation { get; set; }
        }

        public class ConfirmedRoomDto
        {
            public string Name { get; set; }
            public double Width { get; set; }
            public double Length { get; set; }
            public double PositionX { get; set; }
            public double PositionY { get; set; }
            public List<ConfirmedFurnitureDto> Furniture { get; set; } = new List<ConfirmedFurnitureDto>();
        }

        public class ConfirmedLayoutRequest
        {
            public List<Point3D> BuildingOuterWall { get; set; }
            public List<ConfirmedRoomDto> Rooms { get; set; }
        }

        // קריאת שדה מספרי לפי שם (אם אין שדה כזה ב-DTO מקבלים null ולא שגיאת קומפילציה)
        private static double? Num(object obj, string prop)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(prop);
            if (p == null) return null;
            var v = p.GetValue(obj);
            if (v == null) return null;
            try { return Convert.ToDouble(v); } catch { return null; }
        }

        [HttpPost("export-confirmed-layout-zip")]
        public IActionResult ExportConfirmedLayoutZip([FromBody] ConfirmedLayoutRequest request)
        {
            if (request == null || request.Rooms == null || request.Rooms.Count == 0 || request.BuildingOuterWall == null || request.BuildingOuterWall.Count == 0)
            {
                return BadRequest("לא התקבלו נתוני תוכנית ליצוא. ודאי שיש תוכנית ריהוט מאושרת על המסך לפני ההורדה.");
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), $"ArchitecturalProject_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);
            string zipFilePath = tempFolder + ".zip";

            try
            {
                var doc = new netDxf.DxfDocument();
                List<netDxf.Entities.Line> exteriorWalls = new List<netDxf.Entities.Line>();
                var wallLayer = new netDxf.Tables.Layer("Exterior_Walls");

                for (int i = 0; i < request.BuildingOuterWall.Count; i++)
                {
                    var pStart = request.BuildingOuterWall[i];
                    var pEnd = request.BuildingOuterWall[(i + 1) % request.BuildingOuterWall.Count];
                    var line = new netDxf.Entities.Line(new netDxf.Vector2(pStart.X, pStart.Y), new netDxf.Vector2(pEnd.X, pEnd.Y)) { Layer = wallLayer };
                    exteriorWalls.Add(line);
                    doc.Entities.Add(line);
                }

                var roomLayer = new netDxf.Tables.Layer("A-WALL-INT") { Color = new netDxf.AciColor(210) };
                if (!doc.Layers.Contains(roomLayer.Name)) doc.Layers.Add(roomLayer);

                var furnitureLayer = new netDxf.Tables.Layer("A-FURN") { Color = netDxf.AciColor.Magenta };
                if (!doc.Layers.Contains(furnitureLayer.Name)) doc.Layers.Add(furnitureLayer);

                string blocksRootFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Blocks");

                var textLayer = new netDxf.Tables.Layer("A-TEXT") { Color = netDxf.AciColor.Yellow };
                if (!doc.Layers.Contains(textLayer.Name)) doc.Layers.Add(textLayer);

                foreach (var room in request.Rooms)
                {
                    var rp1 = new netDxf.Vector2(room.PositionX, room.PositionY);
                    var rp2 = new netDxf.Vector2(room.PositionX + room.Width, room.PositionY);
                    var rp3 = new netDxf.Vector2(room.PositionX + room.Width, room.PositionY + room.Length);
                    var rp4 = new netDxf.Vector2(room.PositionX, room.PositionY + room.Length);
                    doc.Entities.Add(new netDxf.Entities.Polyline2D(new List<netDxf.Vector2> { rp1, rp2, rp3, rp4, rp1 }) { Layer = roomLayer });

                    // 🌟 תווית שם החדר
                    doc.Entities.Add(new netDxf.Entities.Text($"{room.Name}", new netDxf.Vector2(room.PositionX + 0.1, room.PositionY + room.Length - 0.3), 0.25) { Layer = textLayer });

                    foreach (var f in room.Furniture ?? new List<ConfirmedFurnitureDto>())
                    {
                        double startX = room.PositionX + f.X;
                        double startY = room.PositionY + f.Y;
                        string matchedBlockPath = null;

                        if (Directory.Exists(blocksRootFolder))
                        {
                            var exactMatches = Directory.GetFiles(blocksRootFolder, $"{f.Name}.dxf", SearchOption.AllDirectories);
                            if (exactMatches.Length > 0) matchedBlockPath = exactMatches[0];
                            else
                            {
                                string firstWord = (f.Name ?? "").Split(' ')[0];
                                var partialMatches = Directory.GetFiles(blocksRootFolder, $"*{firstWord}*.dxf", SearchOption.AllDirectories);
                                if (partialMatches.Length > 0) matchedBlockPath = partialMatches[0];
                            }
                        }

                        if (matchedBlockPath != null && System.IO.File.Exists(matchedBlockPath))
                        {
                            try
                            {
                                DxfDocument blockDoc = DxfDocument.Load(matchedBlockPath);
                                string safeBlockName = $"B_{(f.Name ?? "item").Replace(" ", "_").GetHashCode()}";
                                if (!doc.Blocks.Contains(safeBlockName))
                                {
                                    var newBlock = new netDxf.Blocks.Block(safeBlockName);
                                    foreach (var entity in blockDoc.Entities.All) newBlock.Entities.Add((netDxf.Entities.EntityObject)entity.Clone());
                                    doc.Blocks.Add(newBlock);
                                }
                                doc.Entities.Add(new netDxf.Entities.Insert(doc.Blocks[safeBlockName], new netDxf.Vector2(startX, startY)) { Layer = furnitureLayer, Rotation = f.Rotation });
                            }
                            catch { DrawFallbackRectangle(doc, startX, startY, f.Width, f.Length, furnitureLayer); }
                        }
                        else { DrawFallbackRectangle(doc, startX, startY, f.Width, f.Length, furnitureLayer); }

                        //  תווית שם הרהיט
                        doc.Entities.Add(new netDxf.Entities.Text(f.Name ?? "", new netDxf.Vector2(startX, startY - 0.1), 0.12) { Layer = textLayer });
                    }
                }

                string baseFurniturePath = Path.Combine(tempFolder, "1_Furniture_Plan.dxf");
                BL.Algorithms.HebrewFontFixer.ApplyToDoc(doc);
                doc.Save(baseFurniturePath);

                // ===== מודל נקי לחזיתות ולחתכים (מטרים) =====
                var geo = new BL.Algorithms.BuildingGeometry();
                foreach (var p in request.BuildingOuterWall) geo.Outline.Add(new netDxf.Vector2(p.X, p.Y));
                foreach (var room in request.Rooms)
                {
                    var ri = new BL.Algorithms.RoomInfo { Name = room.Name ?? "", X = room.PositionX, Y = room.PositionY, W = room.Width, L = room.Length };
                    foreach (var f in room.Furniture ?? new List<ConfirmedFurnitureDto>())
                    {
                        double fw = f.Width, fln = f.Length;
                        double rot = Math.Abs(f.Rotation % 180);
                        if (rot > 45 && rot < 135) { double t = fw; fw = fln; fln = t; }
                        double fx = room.PositionX + f.X, fy = room.PositionY + f.Y;
                        ri.Furniture.Add(new BL.Algorithms.FurnInfo { Name = f.Name ?? "", X0 = fx, Y0 = fy, X1 = fx + fw, Y1 = fy + fln });
                    }
                    geo.Rooms.Add(ri);

                    void AddOpening(string px, string py, string psize, bool isDoor, double defW)
                    {
                        double? ox = Num(room, px), oy = Num(room, py), os = Num(room, psize);
                        if (!ox.HasValue || !oy.HasValue || (ox.Value == 0 && oy.Value == 0)) return;
                        double ax = ox.Value, ay = oy.Value;
                        bool absolute = ax >= room.PositionX - 0.6 && ax <= room.PositionX + room.Width + 0.6 &&
                                        ay >= room.PositionY - 0.6 && ay <= room.PositionY + room.Length + 0.6;
                        if (!absolute) { ax += room.PositionX; ay += room.PositionY; }
                        double w = (os.HasValue && os.Value > 0) ? os.Value : defW;
                        if (w > 5) w /= 100.0; // אם הגיע בס"מ
                        geo.Openings.Add(new BL.Algorithms.OpeningInfo { X = ax, Y = ay, Width = w, IsDoor = isDoor, RoomName = room.Name ?? "" });
                    }
                    AddOpening("DoorX", "DoorY", "DoorSize", true, 0.9);
                    AddOpening("WindowX", "WindowY", "WindowSize", false, 1.2);
                }
                Console.WriteLine($"ℹ️ חזיתות/חתכים: {geo.Rooms.Count} חדרים, {geo.Openings.Count} פתחים");

                try
                {
                    var dimService = new BL.Algorithms.DimensionsService();
                    string dimPath = dimService.GenerateDimensionsPlan(geo);
                    if (!dimPath.StartsWith("Error"))
                    {
                        try
                        {
                            var dimDoc = netDxf.DxfDocument.Load(dimPath);
                            dimService.AddInteriorDimensions(dimDoc, geo);   // שרטוט מידות פנימיות מעל התוכנית
                            dimDoc.Save(dimPath);
                        }
                        catch (Exception exI) { Console.WriteLine($"⚠️ מידות פנימיות נכשלו (מדלגים): {exI.Message}"); }
                        System.IO.File.Copy(dimPath, Path.Combine(tempFolder, "2_Dimensions_Plan.dxf"));
                    }
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ שגיאה ביצירת תוכנית מידות (מדלגים): {ex.Message}"); }

                try
                {
                    var elevService = new BL.Algorithms.ElevationService();
                    string elevPath = elevService.GenerateElevationsPlan(geo);
                    if (!elevPath.StartsWith("Error")) System.IO.File.Copy(elevPath, Path.Combine(tempFolder, "3_Elevations_Plan.dxf"));
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ שגיאה ביצירת חזיתות (מדלגים): {ex.Message}"); }

                try
                {
                    var secGenerator = new BL.Algorithms.SectionGenerator();
                    var sections = secGenerator.GenerateAutoSections(geo);
                    if (sections.ContainsKey("Section_AA")) sections["Section_AA"].Save(Path.Combine(tempFolder, "4_Section_AA.dxf"));
                    if (sections.ContainsKey("Section_BB")) sections["Section_BB"].Save(Path.Combine(tempFolder, "4_Section_BB.dxf"));
                
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ שגיאה ביצירת חתך (מדלגים): {ex.Message}"); }

                try
                {
                    var roofService = new BL.Algorithms.RoofService();
                    var roofSpec = new BL.Algorithms.RoofService.RoofSpecification { RoofType = "parapet", SlopePercent = 1.5, WallThickness = 20.0 };
                    string roofPath = roofService.GenerateRoofPlanFromGeometry(geo, roofSpec);
                    if (!roofPath.StartsWith("Error")) System.IO.File.Copy(roofPath, Path.Combine(tempFolder, "5_Roof_Plan.dxf"));
                }
                catch (Exception ex) { Console.WriteLine($"⚠️ שגיאה ביצירת גג (מדלגים): {ex.Message}"); }

                ZipFile.CreateFromDirectory(tempFolder, zipFilePath);
                var fileBytes = System.IO.File.ReadAllBytes(zipFilePath);
                return File(fileBytes, "application/zip", "Full_Architectural_Project.zip");
            }
            catch (Exception ex) { return StatusCode(500, $"שגיאה בהפקת הפרויקט: {ex.Message}"); }
            finally
            {
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                if (System.IO.File.Exists(zipFilePath)) System.IO.File.Delete(zipFilePath);
            }
        }

        [HttpPost("generate-layout")]
        public IActionResult GenerateFullLayout([FromBody] LayoutRequest request)
        {
            Console.WriteLine("🚀 מפעיל את אלגוריתם הפריסה...");

            if (request == null || request.PlotVertices == null || request.RequestedRooms == null)
            {
                return BadRequest("נתוני בקשה חסרים או שגויים.");
            }

            try
            {
                var freshRequestedRooms = new List<CadApplication.Algorithms.ArchitecturalRoom>();
                foreach (var r in request.RequestedRooms)
                {
                    string roomName = string.IsNullOrEmpty(r.Name) ? "חדר כללי" : r.Name;
                    freshRequestedRooms.Add(new CadApplication.Algorithms.ArchitecturalRoom
                    {
                        Name = roomName,
                        Area = NormalizeRoomArea(roomName, r.Area), // 🌟 שטח מוגבל לטווח הגיוני לפי סוג החדר
                        PositionX = 0,
                        PositionY = 0,
                        PreferredNeighbors = r.PreferredNeighbors ?? new List<string>()
                    });
                }

                Console.WriteLine("📐 מחשב מעטפת וחותך את המגרש (BSP)...");
                List<Point3D> buildingFootprint = _layoutService.GenerateBuildingFootprint(request.PlotVertices, 3.0);

                // 🌟 מחשבים כמה שטח באמת דרוש (חדרים + מסדרון משוער), ולא מאלצים למלא את כל המגרש
                double totalRequestedArea = freshRequestedRooms.Sum(r => r.Area);
                double corridorOverhead = freshRequestedRooms.Count >= 3 ? totalRequestedArea * 0.12 : 0;
                double neededArea = totalRequestedArea + corridorOverhead;
                double fullPlotArea = CalculatePolygonAreaHelper(buildingFootprint);

                List<Point3D> buildFootprintForRooms = buildingFootprint;
                if (fullPlotArea > neededArea && neededArea > 0)
                {
                    double shrink = Math.Sqrt(neededArea / fullPlotArea);
                    double cx = buildingFootprint.Average(p => p.X);
                    double cy = buildingFootprint.Average(p => p.Y);
                    buildFootprintForRooms = buildingFootprint.Select(p => new Point3D(0, cx + (p.X - cx) * shrink, cy + (p.Y - cy) * shrink, 0)).ToList();
                }

                LayoutResult layoutResult = _hybridModel.ExecuteHybridGeneration(freshRequestedRooms, buildFootprintForRooms, fullPlotArea);

                if (layoutResult == null || !layoutResult.Success || layoutResult.PlacedRooms == null)
                {
                    return BadRequest("האלגוריתם לא הצליח למקם את החדרים במגרש. נסי להקטין מידות.");
                }

                // 🌟 מאפסים את כל הקואורדינטות כך שיתחילו מ-(0,0)
                double normMinX = buildingFootprint.Min(p => p.X);
                double normMinY = buildingFootprint.Min(p => p.Y);
                foreach (var r in layoutResult.PlacedRooms)
                {
                    r.PositionX -= normMinX;
                    r.PositionY -= normMinY;
                }

                // 🌟 קיר חיצוני "צמוד" - עוטף רק את הדירה בפועל (לא את כל שטח המגרש הגדול), 20 ס"מ מכל צד
                double wallOffset = 0.2;
                double roomsMinX = layoutResult.PlacedRooms.Min(r => r.PositionX);
                double roomsMaxX = layoutResult.PlacedRooms.Max(r => r.PositionX + r.Width);
                double roomsMinY = layoutResult.PlacedRooms.Min(r => r.PositionY);
                double roomsMaxY = layoutResult.PlacedRooms.Max(r => r.PositionY + r.Length);

                List<Point3D> tightOuterWall = new List<Point3D>
                {
                    new Point3D(0, roomsMinX - wallOffset, roomsMinY - wallOffset, 0),
                    new Point3D(0, roomsMaxX + wallOffset, roomsMinY - wallOffset, 0),
                    new Point3D(0, roomsMaxX + wallOffset, roomsMaxY + wallOffset, 0),
                    new Point3D(0, roomsMinX - wallOffset, roomsMaxY + wallOffset, 0)
                };

                // 🌟 הפתחים מחושבים עכשיו מול המעטפת הצמודה - זיהוי קירות חיצוניים מדויק בהרבה
                var openingsPlanner = new BL.Algorithms.OpeningsPlanner();
                var openings = openingsPlanner.GenerateOpenings(layoutResult.PlacedRooms, tightOuterWall);

                Console.WriteLine("🛋️ החדרים מוקמו! מפעיל חישול מדומה למיקום ריהוט...");
                List<object> roomsForFrontend = new List<object>();

                foreach (var archRoom in layoutResult.PlacedRooms)
                {
                    // מוצאים את הדלת האמיתית של החדר הזה (אם יש), ומחשבים את המיקום שלה יחסית לפינת החדר
                    var roomDoor = openings.FirstOrDefault(o => o.RoomName == archRoom.Name && o.Type == "Door");
                    double doorLocalX = roomDoor != null ? roomDoor.X - archRoom.PositionX : -10; // -10 = אין דלת אמיתית לחדר הזה, לא לחסום כלום
                    double doorLocalY = roomDoor != null ? roomDoor.Y - archRoom.PositionY : -10;

                    Room currentRoom = new Room
                    {
                        Name = archRoom.Name,
                        Width = archRoom.Width,
                        Length = archRoom.Length,
                        DoorX = doorLocalX,
                        DoorY = doorLocalY,
                        WindowX = archRoom.Width / 2,
                        WindowY = archRoom.Length,
                        furniture = new List<Furniture>()
                    };

                    InjectDefaultFurnitureForRoom(currentRoom);
                    _placementAlgorithm.RunPlacement(currentRoom);

                    // 🌟 התיקון הקריטי: יצירת אובייקט ריהוט "נקי" כדי שהשרת לא יקרוס בלולאת JSON!
                    var safeFurnitureList = currentRoom.furniture.Select(f => new
                    {
                        Name = f.Name,
                        Width = f.Width,
                        Length = f.Length,
                        X = f.Location.X,
                        Y = f.Location.Y,
                        Rotation = f.Rotation
                    }).ToList();

                    roomsForFrontend.Add(new
                    {
                        Name = currentRoom.Name,
                        Width = currentRoom.Width,
                        Length = currentRoom.Length,
                        PositionX = archRoom.PositionX,
                        PositionY = archRoom.PositionY,
                        X = archRoom.PositionX,
                        Y = archRoom.PositionY,
                        Furniture = safeFurnitureList // שולחים את הרשימה המאובטחת
                    });
                }

                Console.WriteLine("✅ הכל חושב בהצלחה! מחזיר נתונים לריאקט...");
                return Ok(new
                {
                    Success = true,
                    BuildingOuterWall = tightOuterWall,
                    TotalAreaM2 = layoutResult.TotalAreaM2,
                    CoveragePercent = layoutResult.FinalCoveragePercent,
                    Rooms = roomsForFrontend,
                    Openings = openings
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔥 שגיאה קריטית: {ex.Message}");
                return StatusCode(500, $"שגיאה פנימית במהלך הפקת הפרוגרמה: {ex.Message}");
            }
        }

        // 🌟 מגביל כל שטח חדר לטווח הגיוני אדריכלית לפי סוג החדר - זה מה שמונע "שירותים 22 מ"ר"
        private double NormalizeRoomArea(string name, double requestedArea)
        {
            name = name ?? "";
            double min = 6, max = 30; // ברירת מחדל לחדר כללי

            if (name.Contains("שירותים") || name.Contains("שירות")) { min = 1.5; max = 3.5; }
            else if (name.Contains("אמבטיה") || name.Contains("רחצה") || name.Contains("מקלחת")) { min = 3; max = 8; }
            else if (name.Contains("מטבח")) { min = 6; max = 16; }
            else if (name.Contains("סלון") || name.Contains("מגורים")) { min = 14; max = 35; }
            else if (name.Contains("שינה") || name.Contains("הורים") || name.Contains("ילדים") || name.Contains("ממד") || name.Contains("ממ\"ד")) { min = 8; max = 20; }
            else if (name.Contains("מרפסת")) { min = 3; max = 12; }
            else if (name.Contains("עבודה") || name.Contains("משרד")) { min = 6; max = 14; }

            double area = requestedArea <= 0 ? min : requestedArea;
            return Math.Max(min, Math.Min(max, area));
        }

        // עזר לחישוב שטח מצולע (נוסחת Shoelace)
        private double CalculatePolygonAreaHelper(List<Point3D> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 1.0;
            double area = 0;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                var p1 = polygon[i];
                var p2 = polygon[(i + 1) % n];
                area += (p1.X * p2.Y) - (p2.X * p1.Y);
            }
            return Math.Abs(area) / 2.0;
        }

        // פונקציית עזר שמחליטה אילו רהיטים להביא לכל חדר על בסיס השם שלו
        private void InjectDefaultFurnitureForRoom(Room room)
        {
            if (string.IsNullOrEmpty(room.Name)) return;
            // משתמשים במידות בסנטימטרים ומפזרים אותם מעט במיקום ההתחלתי (50, 50) כדי שהאלגוריתם יתחיל לעבוד בנוח

            // 1. חדרי שינה (הורים, ילדים, ממ"ד)
            if (room.Name.Contains("שינה") || room.Name.Contains("הורים") || room.Name.Contains("ילדים") || room.Name.Contains("ממד") || room.Name.Contains("ממ\"ד"))
            {
                room.furniture.Add(new Furniture { Name = "מיטה", Width = 1.60, Length = 2.00, Height = 0.50, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "ארון", Width = 2.00, Length = 0.60, Height = 2.20, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "שידה", Width = 0.50, Length = 0.50, Height = 0.60, Location = new Point3D(0, 0, 0) });
            }
            // 2. סלון / חלל מגורים
            else if (room.Name.Contains("סלון") || room.Name.Contains("מגורים"))
            {
                room.furniture.Add(new Furniture { Name = "ספה", Width = 2.40, Length = 0.90, Height = 0.80, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "שולחן", Width = 1.20, Length = 0.70, Height = 0.40, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "מזנון", Width = 1.80, Length = 0.40, Height = 0.50, Location = new Point3D(0, 0, 0) });
            }
            // 3. מטבח
            else if (room.Name.Contains("מטבח"))
            {
                double totalFixedWidth = 0.90 + 0.80 + 0.60;
                bool fitsAsFixedRow = room.Width >= totalFixedWidth + 0.2; // 🌟 בודקים שהם בכלל נכנסים ברוחב החדר

                if (fitsAsFixedRow)
                {
                    double x = 0.1;
                    var fridge = new Furniture { Name = "מקרר", Width = 0.90, Length = 0.80, Height = 1.90, Location = new Point3D(0, x, 0.05, 0), IsFixed = true };
                    x += fridge.Width;
                    var sink = new Furniture { Name = "כיור מטבח", Width = 0.80, Length = 0.55, Height = 0.85, Location = new Point3D(0, x, 0.05, 0), IsFixed = true };
                    x += sink.Width;
                    var stove = new Furniture { Name = "כיריים", Width = 0.60, Length = 0.60, Height = 0.85, Location = new Point3D(0, x, 0.05, 0), IsFixed = true };

                    room.furniture.Add(fridge);
                    room.furniture.Add(sink);
                    room.furniture.Add(stove);
                }
                else
                {
                    // החדר צר מדי לרצף הקבוע - נותנים לאלגוריתם הרגיל למקם אותם בנפרד (עדיף מחפיפה)
                    room.furniture.Add(new Furniture { Name = "מקרר", Width = 0.90, Length = 0.80, Height = 1.90, Location = new Point3D(0, 0, 0) });
                    room.furniture.Add(new Furniture { Name = "כיור מטבח", Width = 0.80, Length = 0.55, Height = 0.85, Location = new Point3D(0, 0, 0) });
                    room.furniture.Add(new Furniture { Name = "כיריים", Width = 0.60, Length = 0.60, Height = 0.85, Location = new Point3D(0, 0, 0) });
                }

                room.furniture.Add(new Furniture { Name = "שולחן", Width = 1.20, Length = 0.80, Height = 0.75, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "ארון מטבח", Width = 1.80, Length = 0.60, Height = 0.85, Location = new Point3D(0, 0, 0) });
            }
            // שירותים - רק אסלה + כיור קטן
            else if (room.Name.Contains("שירותים"))
            {
                room.furniture.Add(new Furniture { Name = "אסלה", Width = 0.40, Length = 0.65, Height = 0.40, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "כיור", Width = 0.45, Length = 0.35, Height = 0.85, Location = new Point3D(0, 0, 0) });
            }
            // 4ב. חדרי רחצה/אמבטיה/מקלחת - אסלה + אמבטיה/מקלחון + כיור
            else if (room.Name.Contains("אמבטיה") || room.Name.Contains("רחצה") || room.Name.Contains("מקלחת"))
            {
                room.furniture.Add(new Furniture { Name = "אסלה", Width = 0.40, Length = 0.65, Height = 0.40, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "אמבטיה", Width = 0.70, Length = 1.60, Height = 0.50, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "כיור", Width = 0.80, Length = 0.50, Height = 0.85, Location = new Point3D(0, 0, 0) });
            }
            // 5. חדרי עבודה / משרד
            else if (room.Name.Contains("עבודה") || room.Name.Contains("משרד"))
            {
                room.furniture.Add(new Furniture { Name = "שולחן כתיבה", Width = 1.40, Length = 0.70, Height = 0.70, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "כיסא", Width = 0.60, Length = 0.60, Height = 0.90, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "ספריה", Width = 1.20, Length = 0.40, Height = 1.80, Location = new Point3D(0, 0, 0) });
            }
            // 6. מרפסת
            else if (room.Name.Contains("מרפסת"))
            {
                room.furniture.Add(new Furniture { Name = "שולחן", Width = 0.90, Length = 0.90, Height = 0.75, Location = new Point3D(0, 0, 0) });
                room.furniture.Add(new Furniture { Name = "כיסא", Width = 0.55, Length = 0.55, Height = 0.80, Location = new Point3D(0, 0, 0) });
            }
        }

        // מייצרת תוכנית ריהוט לפי הנתונים מריאקט ומורידה את התוכנית
        [HttpPost("export-full-layout-dxf")]
        public IActionResult ExportFullLayoutToDxf([FromBody] LayoutRequest request)
        {
            if (request == null || request.PlotVertices == null || request.RequestedRooms == null)
            {
                return BadRequest("נתונים חסרים לצורך הפקת הקובץ.");
            }

            try
            {
                var freshRequestedRooms = new List<CadApplication.Algorithms.ArchitecturalRoom>();
                foreach (var r in request.RequestedRooms)
                {
                    double w = r.Width > 30 ? r.Width / 100.0 : r.Width;
                    double l = r.Length > 30 ? r.Length / 100.0 : r.Length;

                    freshRequestedRooms.Add(new CadApplication.Algorithms.ArchitecturalRoom
                    {
                        // 🛡️ הגנה 1: טיפול ב-Null Reference אם NLP לא זיהה שם
                        Name = string.IsNullOrEmpty(r.Name) ? "חדר כללי" : r.Name,
                        Width = w,
                        Length = l,
                        PositionX = 0,
                        PositionY = 0,
                        PreferredNeighbors = r.PreferredNeighbors ?? new List<string>()
                    });
                }

                List<Point3D> buildingFootprint = _layoutService.GenerateBuildingFootprint(request.PlotVertices, 3.0);
                LayoutResult layoutResult = _hybridModel.ExecuteHybridGeneration(freshRequestedRooms, buildingFootprint, 500);

                // 🛡️ הגנה 2: מניעת קריסה אם אלגוריתם החלוקה לא הצליח למצוא פתרון גיאומטרי
                if (layoutResult == null || layoutResult.PlacedRooms == null || layoutResult.PlacedRooms.Count == 0)
                {
                    return BadRequest("האלגוריתם לא הצליח למקם את החדרים בתוך שטח המגרש. נסי להקטין את מידות החדרים.");
                }

                List<ArchitecturalRoom> placedRooms = layoutResult.PlacedRooms;
                var doc = new netDxf.DxfDocument();

                for (int i = 0; i < buildingFootprint.Count; i++)
                {
                    var pStart = buildingFootprint[i];
                    var pEnd = buildingFootprint[(i + 1) % buildingFootprint.Count];

                    var wallLine = new netDxf.Entities.Line(
                        new netDxf.Vector2(pStart.X, pStart.Y),
                        new netDxf.Vector2(pEnd.X, pEnd.Y)
                    )
                    { Layer = new netDxf.Tables.Layer("Exterior_Walls") };
                    doc.Entities.Add(wallLine);
                }

                var roomLayer = new netDxf.Tables.Layer("A-WALL-INT") { Color = new netDxf.AciColor(210) };
                if (!doc.Layers.Contains(roomLayer.Name)) doc.Layers.Add(roomLayer);

                var furnitureLayer = new netDxf.Tables.Layer("A-FURN") { Color = netDxf.AciColor.Magenta };
                if (!doc.Layers.Contains(furnitureLayer.Name)) doc.Layers.Add(furnitureLayer);

                foreach (var archRoom in placedRooms)
                {
                    double roomWidth = archRoom.Width;
                    double roomLength = archRoom.Length;
                    double roomGlobalX = archRoom.PositionX;
                    double roomGlobalY = archRoom.PositionY;

                    Room currentRoom = new Room
                    {
                        Name = archRoom.Name,
                        Width = roomWidth,
                        Length = roomLength,
                        furniture = new List<Furniture>()
                    };

                    InjectDefaultFurnitureForRoom(currentRoom);
                    _placementAlgorithm.RunPlacement(currentRoom);

                    var rp1 = new netDxf.Vector2(roomGlobalX, roomGlobalY);
                    var rp2 = new netDxf.Vector2(roomGlobalX + roomWidth, roomGlobalY);
                    var rp3 = new netDxf.Vector2(roomGlobalX + roomWidth, roomGlobalY + roomLength);
                    var rp4 = new netDxf.Vector2(roomGlobalX, roomGlobalY + roomLength);

                    var roomPoly = new netDxf.Entities.Polyline2D(new List<netDxf.Vector2> { rp1, rp2, rp3, rp4, rp1 })
                    {
                        Layer = roomLayer
                    };
                    doc.Entities.Add(roomPoly);

                    string blocksRootFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Blocks");

                    foreach (var f in currentRoom.furniture)
                    {
                        double startX = roomGlobalX + f.Location.X;
                        double startY = roomGlobalY + f.Location.Y;
                        string matchedBlockPath = null;

                        if (Directory.Exists(blocksRootFolder))
                        {
                            var exactMatches = Directory.GetFiles(blocksRootFolder, $"{f.Name}.dxf", SearchOption.AllDirectories);
                            if (exactMatches.Length > 0)
                            {
                                matchedBlockPath = exactMatches[0];
                            }
                            else
                            {
                                string firstWord = f.Name.Split(' ')[0];
                                var partialMatches = Directory.GetFiles(blocksRootFolder, $"*{firstWord}*.dxf", SearchOption.AllDirectories);
                                if (partialMatches.Length > 0)
                                {
                                    matchedBlockPath = partialMatches[0];
                                }
                            }
                        }

                        if (matchedBlockPath != null && System.IO.File.Exists(matchedBlockPath))
                        {
                            try
                            {
                                DxfDocument blockDoc = DxfDocument.Load(matchedBlockPath);
                                string safeBlockName = $"B_{f.Name.Replace(" ", "_").GetHashCode()}";

                                if (!doc.Blocks.Contains(safeBlockName))
                                {
                                    var newBlock = new netDxf.Blocks.Block(safeBlockName);
                                    foreach (var entity in blockDoc.Entities.All)
                                    {
                                        newBlock.Entities.Add((EntityObject)entity.Clone());
                                    }
                                    doc.Blocks.Add(newBlock);
                                }

                                var position = new netDxf.Vector2(startX, startY);
                                var insert = new netDxf.Entities.Insert(doc.Blocks[safeBlockName], position)
                                {
                                    Layer = furnitureLayer,
                                    Rotation = f.Rotation
                                };
                                doc.Entities.Add(insert);
                            }
                            catch (Exception)
                            {
                                DrawFallbackRectangle(doc, startX, startY, f.Width, f.Length, furnitureLayer);
                            }
                        }
                        else
                        {
                            DrawFallbackRectangle(doc, startX, startY, f.Width, f.Length, furnitureLayer);
                        }
                    }
                }

                // 🛡️ הגנה 3: שימוש בנתיב ה-Temp של מערכת ההפעלה במקום שמירה מסוכנת בשורש השרת
                string fileName = Path.Combine(Path.GetTempPath(), $"Furniture_Plan_{Guid.NewGuid()}.dxf");
                doc.Save(fileName);

                var fileBytes = System.IO.File.ReadAllBytes(fileName);
                try { System.IO.File.Delete(fileName); } catch { }

                return File(fileBytes, "application/octet-stream", "Full_Furniture_Plan.dxf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"שגיאה בהפקת קובץ ה-DXF: {ex.Message}");
            }
        }

        // הוצאת תיקיית זיפ
        [HttpPost("export-project-zip")]
        public IActionResult ExportProjectZip([FromBody] LayoutRequest request)
        {
            if (request == null || request.PlotVertices == null || request.RequestedRooms == null)
            {
                return BadRequest("נתונים חסרים לצורך הפקת הפרויקט.");
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), $"ArchitecturalProject_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);
            string zipFilePath = tempFolder + ".zip";

            try
            {
                var freshRequestedRooms = new List<CadApplication.Algorithms.ArchitecturalRoom>();
                foreach (var r in request.RequestedRooms)
                {
                    double w = r.Width > 30 ? r.Width / 100.0 : r.Width;
                    double l = r.Length > 30 ? r.Length / 100.0 : r.Length;
                    freshRequestedRooms.Add(new CadApplication.Algorithms.ArchitecturalRoom
                    {
                        Name = string.IsNullOrEmpty(r.Name) ? "חדר" : r.Name,
                        Width = w,
                        Length = l,
                        PositionX = 0,
                        PositionY = 0,
                        PreferredNeighbors = r.PreferredNeighbors ?? new List<string>()
                    });
                }

                List<Point3D> buildingFootprint = _layoutService.GenerateBuildingFootprint(request.PlotVertices, 3.0);
                LayoutResult layoutResult = _hybridModel.ExecuteHybridGeneration(freshRequestedRooms, buildingFootprint, 500);

                var doc = new netDxf.DxfDocument();
                List<netDxf.Entities.Line> exteriorWalls = new List<netDxf.Entities.Line>();
                var wallLayer = new netDxf.Tables.Layer("Exterior_Walls");

                for (int i = 0; i < buildingFootprint.Count; i++)
                {
                    var pStart = buildingFootprint[i];
                    var pEnd = buildingFootprint[(i + 1) % buildingFootprint.Count];
                    var line = new netDxf.Entities.Line(new netDxf.Vector2(pStart.X, pStart.Y), new netDxf.Vector2(pEnd.X, pEnd.Y)) { Layer = wallLayer };
                    exteriorWalls.Add(line);
                    doc.Entities.Add(line);
                }

                var roomLayer = new netDxf.Tables.Layer("A-WALL-INT") { Color = new netDxf.AciColor(210) };
                if (!doc.Layers.Contains(roomLayer.Name)) doc.Layers.Add(roomLayer);

                var furnitureLayer = new netDxf.Tables.Layer("A-FURN") { Color = netDxf.AciColor.Magenta };
                if (!doc.Layers.Contains(furnitureLayer.Name)) doc.Layers.Add(furnitureLayer);

                string blocksRootFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Blocks");

                // ציור החדרים והריהוט אל תוך ה-DXF
                foreach (var archRoom in layoutResult.PlacedRooms)
                {
                    Room currentRoom = new Room { Name = archRoom.Name, Width = archRoom.Width, Length = archRoom.Length, furniture = new List<Furniture>() };
                    InjectDefaultFurnitureForRoom(currentRoom);
                    _placementAlgorithm.RunPlacement(currentRoom);

                    var rp1 = new netDxf.Vector2(archRoom.PositionX, archRoom.PositionY);
                    var rp2 = new netDxf.Vector2(archRoom.PositionX + archRoom.Width, archRoom.PositionY);
                    var rp3 = new netDxf.Vector2(archRoom.PositionX + archRoom.Width, archRoom.PositionY + archRoom.Length);
                    var rp4 = new netDxf.Vector2(archRoom.PositionX, archRoom.PositionY + archRoom.Length);
                    doc.Entities.Add(new netDxf.Entities.Polyline2D(new List<netDxf.Vector2> { rp1, rp2, rp3, rp4, rp1 }) { Layer = roomLayer });

                    foreach (var f in currentRoom.furniture)
                    {
                        double startX = archRoom.PositionX + f.Location.X;
                        double startY = archRoom.PositionY + f.Location.Y;
                        string matchedBlockPath = null;

                        if (Directory.Exists(blocksRootFolder))
                        {
                            var exactMatches = Directory.GetFiles(blocksRootFolder, $"{f.Name}.dxf", SearchOption.AllDirectories);
                            if (exactMatches.Length > 0) matchedBlockPath = exactMatches[0];
                            else
                            {
                                string firstWord = f.Name.Split(' ')[0];
                                var partialMatches = Directory.GetFiles(blocksRootFolder, $"*{firstWord}*.dxf", SearchOption.AllDirectories);
                                if (partialMatches.Length > 0) matchedBlockPath = partialMatches[0];
                            }
                        }

                        if (matchedBlockPath != null && System.IO.File.Exists(matchedBlockPath))
                        {
                            try
                            {
                                DxfDocument blockDoc = DxfDocument.Load(matchedBlockPath);
                                string safeBlockName = $"B_{f.Name.Replace(" ", "_").GetHashCode()}";
                                if (!doc.Blocks.Contains(safeBlockName))
                                {
                                    var newBlock = new netDxf.Blocks.Block(safeBlockName);
                                    foreach (var entity in blockDoc.Entities.All) newBlock.Entities.Add((netDxf.Entities.EntityObject)entity.Clone());
                                    doc.Blocks.Add(newBlock);
                                }
                                doc.Entities.Add(new netDxf.Entities.Insert(doc.Blocks[safeBlockName], new netDxf.Vector2(startX, startY)) { Layer = furnitureLayer, Rotation = f.Rotation });
                            }
                            catch { DrawFallbackRectangle(doc, startX, startY, f.Width, f.Length, furnitureLayer); }
                        }
                        else { DrawFallbackRectangle(doc, startX, startY, f.Width, f.Length, furnitureLayer); }
                    }
                }

                string baseFurniturePath = Path.Combine(tempFolder, "1_Furniture_Plan.dxf");
                BL.Algorithms.HebrewFontFixer.ApplyToDoc(doc);
                doc.Save(baseFurniturePath);

                var dimService = new BL.Algorithms.DimensionsService();
                string dimPath = dimService.GenerateDimensionsPlan(baseFurniturePath);
                if (!dimPath.StartsWith("Error")) System.IO.File.Copy(dimPath, Path.Combine(tempFolder, "2_Dimensions_Plan.dxf"));

                var elevService = new BL.Algorithms.ElevationService();
                string elevPath = elevService.GenerateElevationsPlan(baseFurniturePath);
                if (!elevPath.StartsWith("Error")) System.IO.File.Copy(elevPath, Path.Combine(tempFolder, "3_Elevations_Plan.dxf"));

                var secGenerator = new BL.Algorithms.SectionGenerator();
                var sections = secGenerator.GenerateAutoSections(doc);
                if (sections.ContainsKey("Section_AA")) sections["Section_AA"].Save(Path.Combine(tempFolder, "4_Section_AA.dxf"));

                var roofService = new BL.Algorithms.RoofService();
                var roofSpec = new BL.Algorithms.RoofService.RoofSpecification { RoofType = "parapet", SlopePercent = 1.5, WallThickness = 20.0 };
                string roofPath = roofService.GenerateRoofPlanFromWalls(exteriorWalls, roofSpec);
                if (!roofPath.StartsWith("Error")) System.IO.File.Copy(roofPath, Path.Combine(tempFolder, "5_Roof_Plan.dxf"));

                foreach (var dxfFile in Directory.GetFiles(tempFolder, "*.dxf"))
                {
                    try { BL.Algorithms.HebrewFontFixer.Apply(dxfFile); }
                    catch (Exception ex) { Console.WriteLine($"⚠️ תיקון פונט נכשל ב-{Path.GetFileName(dxfFile)}: {ex.Message}"); }
                }
                ZipFile.CreateFromDirectory(tempFolder, zipFilePath);
                
                var fileBytes = System.IO.File.ReadAllBytes(zipFilePath);
                return File(fileBytes, "application/zip", "Full_Architectural_Project.zip");
            }
            catch (Exception ex) { return StatusCode(500, $"שגיאה בהפקת הפרויקט: {ex.Message}"); }
            finally
            {
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                if (System.IO.File.Exists(zipFilePath)) System.IO.File.Delete(zipFilePath);
            }
        }

        // פונקציית עזר לציור מלבן גיבוי למקרה שהבלוק נכשל
        private void DrawFallbackRectangle(DxfDocument doc, double x, double y, double w, double l, netDxf.Tables.Layer layer)
        {
            var p1 = new netDxf.Vector2(x, y);
            var p2 = new netDxf.Vector2(x + w, y);
            var p3 = new netDxf.Vector2(x + w, y + l);
            var p4 = new netDxf.Vector2(x, y + l);
            var poly = new netDxf.Entities.Polyline2D(new List<netDxf.Vector2> { p1, p2, p3, p4, p1 }) { Layer = layer };
            doc.Entities.Add(poly);
        }

        // קורא לתוכנית המידות ומחזיר את הקובץ המעובד לריאקט
        [HttpPost("generate-dimensions")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> GenerateDimensions(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("לא נבחר קובץ DXF לייצור מידות.");

            // יצירת נתיב זמני ושמירת השרטוט שעלה
            var tempInputPath = Path.Combine(Path.GetTempPath(), $"Input_{Guid.NewGuid()}.dxf");

            try
            {
                using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // קריאה לשירות המידות שיצרנו
                var dimensionsService = new BL.Algorithms.DimensionsService();
                string outputFilePath = dimensionsService.GenerateDimensionsPlan(tempInputPath);

                if (outputFilePath.StartsWith("Error"))
                {
                    return BadRequest($"שגיאה ביצירת המידות: {outputFilePath}");
                }

                if (!System.IO.File.Exists(outputFilePath))
                {
                    return NotFound("הקובץ המעובד לא נמצא בשרת.");
                }

                // קריאת הקובץ המוכן, מחיקתו מהשרת, ושליחה לריאקט
                var fileBytes = await System.IO.File.ReadAllBytesAsync(outputFilePath);
                System.IO.File.Delete(outputFilePath);

                string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
                return File(fileBytes, "application/dxf", $"{originalFileNameWithoutExt}_With_Dimensions.dxf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"שגיאה פנימית במנוע המידות: {ex.Message}");
            }
            finally
            {
                if (System.IO.File.Exists(tempInputPath))
                {
                    System.IO.File.Delete(tempInputPath);
                }
            }
        }


        // קורא למנוע החזיתות ליצירת חזיתות אדריכליות מדויקות על סמך פתחי תוכנית הריהוט
        [HttpPost("generate-elevations")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> GenerateElevations(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("לא נבחר קובץ DXF לייצור חזיתות.");

            var tempInputPath = Path.Combine(Path.GetTempPath(), $"ElevInput_{Guid.NewGuid()}.dxf");

            try
            {
                using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var elevationService = new BL.Algorithms.ElevationService();
                string outputFilePath = elevationService.GenerateElevationsPlan(tempInputPath);

                if (outputFilePath.StartsWith("Error"))
                {
                    return BadRequest($"שגיאה ביצירת החזיתות: {outputFilePath}");
                }

                if (!System.IO.File.Exists(outputFilePath))
                {
                    return NotFound("קובץ החזיתות המעובד לא נמצא בשרת.");
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(outputFilePath);
                System.IO.File.Delete(outputFilePath);

                string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
                return File(fileBytes, "application/dxf", $"{originalFileNameWithoutExt}_Elevations.dxf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"שגיאה פנימית במנוע החזיתות: {ex.Message}");
            }
            finally
            {
                if (System.IO.File.Exists(tempInputPath))
                {
                    System.IO.File.Delete(tempInputPath);
                }
            }
        }

        // מאפשר למשתמש להזין את המגרש ואת החדרים שהוא רוצה, ומחזיר את התוכנית הסופית עם מיקום החדרים והריהוט
        [HttpPost("generate-bsp-layout")]
        public IActionResult GenerateLayout([FromBody] LayoutRequest request)
        {
            // 1. בדיקה שהמגרש קיים
            if (request.PlotVertices == null || request.PlotVertices.Count == 0)
                return BadRequest("לא התקבלו נתוני מגרש!");

            // 2. הגדרת המשתנה 'plot' - כאן הייתה הטעות שלך
            double minX = request.PlotVertices.Min(p => p.X);
            double maxX = request.PlotVertices.Max(p => p.X);
            double minY = request.PlotVertices.Min(p => p.Y);
            double maxY = request.PlotVertices.Max(p => p.Y);

            var plot = new BL.Algorithms.Rectangle2D // שימוש בנתיב מלא כדי למנוע טעויות
            {
                X = minX,
                Y = minY,
                Width = maxX - minX,
                Length = maxY - minY
            };

            // 3. הגדרת ה-Rooms בצורה בטוחה
            // אם יש שגיאה, תוודאי שהרשימה כאן היא בדיוק מאותו סוג כמו ב-BSPSplitter
            var splitter = new BSPSplitter();
            var result = splitter.Run(plot, request.Rooms);

            return Ok(result);
        }

       
        [HttpPost("generate-sections")]
        [DisableRequestSizeLimit]
        public IActionResult GenerateSections(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("לא נבחר קובץ לביצוע חתך.");

            string tempInputPath = Path.GetTempFileName();
            string tempOutputPath = Path.Combine(Path.GetTempPath(), $"Section_AA_{Guid.NewGuid()}.dxf");

            try
            {
                // שמירת הקובץ זמנית
                using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                // טעינת מסמך ה-DXF המקורי
                DxfDocument planDxf = DxfDocument.Load(tempInputPath);
                if (planDxf == null)
                    return BadRequest("קובץ ה-DXF אינו תקין.");

                // קריאה לשכבת הלוגיקה (BL) ליצירת החתכים האוטומטיים!
                SectionGenerator generator = new SectionGenerator();

                // הפונקציה מחזירה מילון עם 2 החתכים
                var sections = generator.GenerateAutoSections(planDxf);

                // לשם הפשטות - נחזיר כרגע למשתמש רק את קובץ ה-DXF של חתך AA (האופקי)
                // (אם תרצי, נוכל בהמשך לעדכן שיחזיר קובץ ZIP שמכיל את שניהם)
                DxfDocument sectionAA = sections["Section_AA"];

                // שמירה והחזרה של הקובץ החדש
                sectionAA.Save(tempOutputPath);
                var bytes = System.IO.File.ReadAllBytes(tempOutputPath);

                return File(bytes, "application/dxf", "Section_AA.dxf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"שגיאה פנימית ביצירת החתך: {ex.Message}");
            }
            finally
            {
                // מחיקת קבצים זמניים - תמיד טוב לשים ב-finally כדי שימחק גם במקרה של שגיאה
                if (System.IO.File.Exists(tempInputPath)) System.IO.File.Delete(tempInputPath);
                if (System.IO.File.Exists(tempOutputPath)) System.IO.File.Delete(tempOutputPath);
            }
        }
    }
}