using System;
using netDxf;
using netDxf.Entities;
using netDxf.Tables;

// תוכנית גגות
public static class DxfHelper
{
    // שליפה או יצירה של שכבה
    public static Layer GetOrCreateLayer(DxfDocument doc, string layerName, AciColor color, Linetype linetype = null)
    {
        if (doc.Layers.Contains(layerName))
        {
            return doc.Layers[layerName];
        }

        Layer newLayer = new Layer(layerName) { Color = color };
        if (linetype != null) newLayer.Linetype = linetype;
        doc.Layers.Add(newLayer);
        return newLayer;
    }

    // טעינת סוג קו מקווקו תקני למסמך
    public static netDxf.Tables.Linetype GetDashedLinetype(DxfDocument doc)
    {
        if (doc.Linetypes.Contains("Dashed"))
        {
            return doc.Linetypes["Dashed"];
        }

        var dashed = netDxf.Tables.Linetype.Dashed;

        if (!doc.Linetypes.Contains(dashed.Name))
        {
            doc.Linetypes.Add(dashed);
        }

        return dashed;
    }

    public static void AddHorizontalDimension(DxfDocument doc, Vector2 start, Vector2 end, double offset, Layer layer)
    {
        double dimY = start.Y + offset;
        LinearDimension dim = new LinearDimension(start, end, dimY, 0.0);
        dim.Layer = layer;

        dim.Style = DimensionStyle.Default;
        dim.Style.ArrowSize = 8.0;
        dim.Style.TextHeight = 10.0;
        dim.Style.TextOffset = 2.0;

        doc.Entities.Add(dim);
    }

    public static void AddVerticalDimension(DxfDocument doc, Vector2 start, Vector2 end, double offset, Layer layer)
    {
        double dimX = start.X + offset;
        LinearDimension dim = new LinearDimension(start, end, dimX, 90.0);
        dim.Layer = layer;

        dim.Style = DimensionStyle.Default;
        dim.Style.ArrowSize = 8.0;
        dim.Style.TextHeight = 10.0;
        dim.Style.TextOffset = 2.0;

        doc.Entities.Add(dim);
    }

    // שרטוט צמ"גים 
    public static void DrawProfessionalDrain(DxfDocument doc, double x, double y, Layer layer)
    {
        Vector2 center = new Vector2(x, y);

        var outerCircle = new netDxf.Entities.Circle(center, 15.0) { Layer = layer };
        var innerCircle = new netDxf.Entities.Circle(center, 5.0) { Layer = layer };
        doc.Entities.Add(outerCircle);
        doc.Entities.Add(innerCircle);

        var lineTop = new netDxf.Entities.Line(new Vector2(x, y + 5.0), new Vector2(x, y + 15.0)) { Layer = layer };
        var lineBottom = new netDxf.Entities.Line(new Vector2(x, y - 5.0), new Vector2(x, y - 15.0)) { Layer = layer };
        var lineLeft = new netDxf.Entities.Line(new Vector2(x - 5.0, y), new Vector2(x - 15.0, y)) { Layer = layer };
        var lineRight = new netDxf.Entities.Line(new Vector2(x + 5.0, y), new Vector2(x + 15.0, y)) { Layer = layer };

        doc.Entities.Add(lineTop);
        doc.Entities.Add(lineBottom);
        doc.Entities.Add(lineLeft);
        doc.Entities.Add(lineRight);

        var text = new netDxf.Entities.Text("צמ\"ג  Ø\"4", new Vector2(x + 20.0, y - 5.0), 12.0) { Layer = layer };
        doc.Entities.Add(text);
    }

    // שרטוט חיצי שיפוע
    public static void DrawSlopeArrow(DxfDocument doc, Vector2 start, Vector2 end, string slopeText, Layer layer)
    {
        var mainLine = new netDxf.Entities.Line(start, end) { Layer = layer };
        doc.Entities.Add(mainLine);

        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double arrowLength = 15.0;
        double arrowAngle = 0.4;

        Vector2 arrowTip1 = new Vector2(
            end.X - arrowLength * Math.Cos(angle - arrowAngle),
            end.Y - arrowLength * Math.Sin(angle - arrowAngle)
        );
        Vector2 arrowTip2 = new Vector2(
            end.X - arrowLength * Math.Cos(angle + arrowAngle),
            end.Y - arrowLength * Math.Sin(angle + arrowAngle)
        );

        var side1 = new netDxf.Entities.Line(end, arrowTip1) { Layer = layer };
        var side2 = new netDxf.Entities.Line(end, arrowTip2) { Layer = layer };
        doc.Entities.Add(side1);
        doc.Entities.Add(side2);

        Vector2 midPoint = new Vector2((start.X + end.X) / 2, ((start.Y + end.Y) / 2) + 10.0);
        var text = new netDxf.Entities.Text(slopeText, midPoint, 12.0) { Layer = layer };
        doc.Entities.Add(text);
    }

    // כתיבת המפלס וסימון אדריכלי
    public static void DrawElevationMarker(DxfDocument doc, double x, double y, string elevationText, Layer layer)
    {
        Vector2 center = new Vector2(x, y);
        double r = 12.0;

        var circle = new netDxf.Entities.Circle(center, r) { Layer = layer };
        var hLine = new netDxf.Entities.Line(new Vector2(x - r - 5, y), new Vector2(x + r + 5, y)) { Layer = layer };
        var vLine = new netDxf.Entities.Line(new Vector2(x, y - r), new Vector2(x, y + r)) { Layer = layer };

        doc.Entities.Add(circle);
        doc.Entities.Add(hLine);
        doc.Entities.Add(vLine);

        var fill1 = new netDxf.Entities.Line(center, new Vector2(x + r * 0.7, y + r * 0.7)) { Layer = layer };
        var fill2 = new netDxf.Entities.Line(center, new Vector2(x - r * 0.7, y - r * 0.7)) { Layer = layer };
        doc.Entities.Add(fill1);
        doc.Entities.Add(fill2);

        var text = new netDxf.Entities.Text(elevationText, new Vector2(x + r + 5, y + 2), 12.0) { Layer = layer };
        doc.Entities.Add(text);
    }

    // שרטוט קווי גלי / גאיות
    public static void DrawValleyLine(DxfDocument doc, Vector2 corner, Vector2 drainPos, Layer layer)
    {
        var valleyLine = new netDxf.Entities.Line(corner, drainPos) { Layer = layer };
        doc.Entities.Add(valleyLine);
    }
}