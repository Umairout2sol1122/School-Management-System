using System.Globalization;
using System.Text;

namespace SchoolManagement.Infrastructure.Services.Certificates;

public class PdfDocumentBuilder
{
    private readonly StringBuilder _stream = new();
    private readonly List<long> _objectOffsets = new();
    private readonly MemoryStream _output = new();

    public static byte[] BuildCertificatePdf(
        string organizationName,
        string? branchName,
        string certificateTitle,
        string serialNumber,
        DateTime issueDate,
        string studentName,
        string? fatherName,
        string admissionNumber,
        string? rollNumber,
        string? className,
        string? sectionName,
        DateTime? dateOfBirth,
        string conduct,
        string? leavingReason,
        string? promotedToClass,
        string? remarks,
        string verificationCode,
        string verificationUrl,
        string? issuedByName)
    {
        var builder = new PdfDocumentBuilder();
        return builder.GenerateLandscapeCertificate(
            organizationName,
            branchName,
            certificateTitle,
            serialNumber,
            issueDate,
            studentName,
            fatherName,
            admissionNumber,
            rollNumber,
            className,
            sectionName,
            dateOfBirth,
            conduct,
            leavingReason,
            promotedToClass,
            remarks,
            verificationCode,
            verificationUrl,
            issuedByName);
    }

    private byte[] GenerateLandscapeCertificate(
        string organizationName,
        string? branchName,
        string certificateTitle,
        string serialNumber,
        DateTime issueDate,
        string studentName,
        string? fatherName,
        string admissionNumber,
        string? rollNumber,
        string? className,
        string? sectionName,
        DateTime? dateOfBirth,
        string conduct,
        string? leavingReason,
        string? promotedToClass,
        string? remarks,
        string verificationCode,
        string verificationUrl,
        string? issuedByName)
    {
        // Dimensions: A4 Landscape = 842 pt width x 595 pt height
        const double width = 842;
        const double height = 595;

        var content = new StringBuilder();

        // 1. Double ornamental borders
        // Outer Navy Border (3pt)
        content.AppendLine("q");
        content.AppendLine("0.10 0.22 0.42 RG"); // Navy Blue
        content.AppendLine("3 w");
        content.AppendLine("28 28 786 539 re S");
        content.AppendLine("Q");

        // Inner Gold Border (1.5pt)
        content.AppendLine("q");
        content.AppendLine("0.80 0.65 0.20 RG"); // Gold
        content.AppendLine("1.5 w");
        content.AppendLine("36 36 770 523 re S");
        content.AppendLine("Q");

        // Corner Ornamental Accents
        DrawCornerAccent(content, 36, 36);
        DrawCornerAccent(content, 806, 36);
        DrawCornerAccent(content, 36, 559);
        DrawCornerAccent(content, 806, 559);

        // Header Divider Line (Gold)
        content.AppendLine("q");
        content.AppendLine("0.80 0.65 0.20 RG");
        content.AppendLine("1.2 w");
        content.AppendLine("120 482 m 722 482 l S");
        content.AppendLine("Q");

        // 2. Organization & Campus Header (Centered)
        DrawText(content, organizationName.ToUpperInvariant(), "F2", 24, 0.10, 0.22, 0.42, width / 2, 525, center: true);
        string campusLine = !string.IsNullOrWhiteSpace(branchName)
            ? $"{branchName} | Islamic Republic of Pakistan"
            : "Quality Education & Character Building";
        DrawText(content, campusLine, "F1", 11, 0.40, 0.40, 0.40, width / 2, 502, center: true);
        DrawText(content, "OFFICIAL ACADEMIC CREDENTIAL", "F2", 9, 0.65, 0.50, 0.15, width / 2, 488, center: true);

        // 3. Certificate Title Banner Box
        content.AppendLine("q");
        content.AppendLine("0.10 0.22 0.42 rg");
        content.AppendLine("220 440 402 32 re f");
        content.AppendLine("Q");
        DrawText(content, certificateTitle.ToUpperInvariant(), "F2", 15, 1.0, 1.0, 1.0, width / 2, 451, center: true);

        // 4. Meta Bar: Serial Number & Issue Date
        DrawText(content, $"Certificate No: {serialNumber}", "F2", 10, 0.2, 0.2, 0.2, 55, 420);
        DrawText(content, $"Date of Issue: {issueDate:dd-MMM-yyyy}", "F2", 10, 0.2, 0.2, 0.2, 650, 420);

        // Thin Separator
        content.AppendLine("q 0.85 0.85 0.85 RG 0.8 w 55 412 m 787 412 l S Q");

        // 5. Certificate Body Text
        DrawText(content, "This is to certify that", "F3", 13, 0.35, 0.35, 0.35, width / 2, 388, center: true);

        // Student Name in Prominent Bold
        DrawText(content, studentName.ToUpperInvariant(), "F4", 20, 0.10, 0.22, 0.42, width / 2, 362, center: true);

        // Father's Name & Admission No
        string lineage = !string.IsNullOrWhiteSpace(fatherName) ? $"Son / Daughter of {fatherName}" : "Student of this Institution";
        DrawText(content, lineage, "F3", 13, 0.25, 0.25, 0.25, width / 2, 342, center: true);

        // Key Details Table / Grid (2 Columns)
        double tableY = 308;
        double col1X = 75;
        double col2X = 450;
        double lineSpacing = 24;

        // Row 1
        DrawDetailField(content, "Admission No:", admissionNumber, col1X, tableY);
        DrawDetailField(content, "Roll No / ID:", rollNumber ?? "N/A", col2X, tableY);

        // Row 2
        tableY -= lineSpacing;
        string classSec = !string.IsNullOrWhiteSpace(sectionName) ? $"{className} (Section {sectionName})" : (className ?? "N/A");
        DrawDetailField(content, "Class & Section:", classSec, col1X, tableY);
        string dobStr = dateOfBirth.HasValue ? dateOfBirth.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : "As per record";
        DrawDetailField(content, "Date of Birth:", dobStr, col2X, tableY);

        // Row 3
        tableY -= lineSpacing;
        DrawDetailField(content, "Character & Conduct:", conduct, col1X, tableY);
        DrawDetailField(content, "Promoted to Class:", promotedToClass ?? "Completed Course", col2X, tableY);

        // Row 4 (Reason for leaving if any)
        if (!string.IsNullOrWhiteSpace(leavingReason))
        {
            tableY -= lineSpacing;
            DrawDetailField(content, "Reason for Leaving:", leavingReason, col1X, tableY);
        }

        // Remarks / Statement
        tableY -= lineSpacing + 4;
        string remarksText = !string.IsNullOrWhiteSpace(remarks)
            ? $"Remarks: {remarks}"
            : "He/She bears a good moral character and has shown sincere dedication during his/her tenure at this institution.";
        DrawText(content, remarksText, "F3", 11, 0.3, 0.3, 0.3, width / 2, tableY, center: true);

        // 6. Verification Box (Left Bottom)
        double verifyBoxX = 55;
        double verifyBoxY = 65;
        double verifyBoxW = 260;
        double verifyBoxH = 75;

        content.AppendLine("q");
        content.AppendLine("0.96 0.97 0.98 rg");
        content.AppendLine($"{verifyBoxX.ToString("F1", CultureInfo.InvariantCulture)} {verifyBoxY.ToString("F1", CultureInfo.InvariantCulture)} {verifyBoxW.ToString("F1", CultureInfo.InvariantCulture)} {verifyBoxH.ToString("F1", CultureInfo.InvariantCulture)} re f");
        content.AppendLine("0.80 0.65 0.20 RG 1 w");
        content.AppendLine($"{verifyBoxX.ToString("F1", CultureInfo.InvariantCulture)} {verifyBoxY.ToString("F1", CultureInfo.InvariantCulture)} {verifyBoxW.ToString("F1", CultureInfo.InvariantCulture)} {verifyBoxH.ToString("F1", CultureInfo.InvariantCulture)} re S");
        content.AppendLine("Q");

        DrawText(content, "DIGITAL VERIFICATION", "F2", 9, 0.10, 0.22, 0.42, verifyBoxX + 10, verifyBoxY + 58);
        DrawText(content, $"Verification Code: {verificationCode}", "F2", 8, 0.2, 0.2, 0.2, verifyBoxX + 10, verifyBoxY + 42);
        DrawText(content, "Scan QR or verify authenticity online at:", "F1", 7.5, 0.4, 0.4, 0.4, verifyBoxX + 10, verifyBoxY + 28);
        DrawText(content, verificationUrl, "F1", 7, 0.10, 0.30, 0.65, verifyBoxX + 10, verifyBoxY + 14);

        // 7. Official Seal Placeholder (Center Bottom)
        double sealX = width / 2;
        double sealY = 98;
        content.AppendLine("q");
        content.AppendLine("0.80 0.65 0.20 RG 1 w");
        // Circle approximation using 4 beziers
        DrawCircle(content, sealX, sealY, 32);
        content.AppendLine("Q");
        DrawText(content, "OFFICIAL", "F2", 7.5, 0.65, 0.50, 0.15, sealX, sealY + 4, center: true);
        DrawText(content, "SEAL", "F2", 7.5, 0.65, 0.50, 0.15, sealX, sealY - 8, center: true);

        // 8. Signatures Block (Right Bottom)
        double sigY = 90;
        double sigLineW = 140;

        // Class Teacher / Prepared by
        double teacherSigX = 500;
        content.AppendLine($"q 0.5 0.5 0.5 RG 0.8 w {teacherSigX} {sigY} m {teacherSigX + sigLineW} {sigY} l S Q");
        DrawText(content, "Class Incharge / Clerk", "F1", 9, 0.3, 0.3, 0.3, teacherSigX + (sigLineW / 2), sigY - 14, center: true);

        // Principal / Head of Institution
        double principalSigX = 660;
        content.AppendLine($"q 0.10 0.22 0.42 RG 1 w {principalSigX} {sigY} m {principalSigX + sigLineW} {sigY} l S Q");
        string signatoryTitle = !string.IsNullOrWhiteSpace(issuedByName) ? issuedByName : "Head of Institution / Principal";
        DrawText(content, signatoryTitle, "F2", 9, 0.10, 0.22, 0.42, principalSigX + (sigLineW / 2), sigY - 14, center: true);

        return AssemblePdf(content.ToString(), (int)width, (int)height);
    }

    private static void DrawCornerAccent(StringBuilder sb, double x, double y)
    {
        sb.AppendLine("q 0.80 0.65 0.20 RG 1 w");
        sb.AppendLine($"{x - 6} {y - 6} 12 12 re S");
        sb.AppendLine("Q");
    }

    private static void DrawCircle(StringBuilder sb, double cx, double cy, double r)
    {
        const double k = 0.552284749831;
        double kr = k * r;
        sb.AppendLine($"{cx + r} {cy} m");
        sb.AppendLine($"{cx + r} {cy + kr} {cx + kr} {cy + r} {cx} {cy + r} c");
        sb.AppendLine($"{cx - kr} {cy + r} {cx - r} {cy + kr} {cx - r} {cy} c");
        sb.AppendLine($"{cx - r} {cy - kr} {cx - kr} {cy - r} {cx} {cy - r} c");
        sb.AppendLine($"{cx + kr} {cy - r} {cx + r} {cy - kr} {cx + r} {cy} c");
        sb.AppendLine("S");
    }

    private static void DrawDetailField(StringBuilder sb, string label, string value, double x, double y)
    {
        DrawText(sb, label, "F2", 11, 0.35, 0.35, 0.35, x, y);
        DrawText(sb, value, "F4", 11.5, 0.1, 0.1, 0.1, x + 130, y);
    }

    private static void DrawText(
        StringBuilder sb,
        string text,
        string font,
        double size,
        double r,
        double g,
        double b,
        double x,
        double y,
        bool center = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        string sanitized = SanitizePdfText(text);

        // Estimate width for centering if required (~0.52 width ratio for standard sans-serif)
        double posX = x;
        if (center)
        {
            double estimatedWidth = sanitized.Length * size * 0.52;
            posX = x - (estimatedWidth / 2);
            if (posX < 40) posX = 40;
        }

        sb.AppendLine("BT");
        sb.AppendLine($"/{font} {size.ToString("F1", CultureInfo.InvariantCulture)} Tf");
        sb.AppendLine($"{r.ToString("F2", CultureInfo.InvariantCulture)} {g.ToString("F2", CultureInfo.InvariantCulture)} {b.ToString("F2", CultureInfo.InvariantCulture)} rg");
        sb.AppendLine($"{posX.ToString("F1", CultureInfo.InvariantCulture)} {y.ToString("F1", CultureInfo.InvariantCulture)} Td");
        sb.AppendLine($"({sanitized}) Tj");
        sb.AppendLine("ET");
    }

    private static string SanitizePdfText(string text)
    {
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            if (c == '(') sb.Append(@"\(");
            else if (c == ')') sb.Append(@"\)");
            else if (c == '\\') sb.Append(@"\\");
            else if (c >= 32 && c <= 126) sb.Append(c);
            else sb.Append(' '); // replace non-ASCII with space for Type 1 compatibility
        }
        return sb.ToString();
    }

    private byte[] AssemblePdf(string contentStream, int width, int height)
    {
        _stream.Clear();
        _objectOffsets.Clear();
        _output.SetLength(0);

        WriteRaw("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        // Object 1: Catalog
        StartObject(1);
        WriteRaw("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Object 2: Pages
        StartObject(2);
        WriteRaw("<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // Object 3: Page
        StartObject(3);
        WriteRaw($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 4 0 R /F2 5 0 R /F3 6 0 R /F4 7 0 R >> >> /Contents 8 0 R >>\nendobj\n");

        // Object 4: Font F1 (Helvetica)
        StartObject(4);
        WriteRaw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        // Object 5: Font F2 (Helvetica-Bold)
        StartObject(5);
        WriteRaw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");

        // Object 6: Font F3 (Times-Roman)
        StartObject(6);
        WriteRaw("<< /Type /Font /Subtype /Type1 /BaseFont /Times-Roman >>\nendobj\n");

        // Object 7: Font F4 (Times-Bold)
        StartObject(7);
        WriteRaw("<< /Type /Font /Subtype /Type1 /BaseFont /Times-Bold >>\nendobj\n");

        // Object 8: Content Stream
        byte[] contentBytes = Encoding.ASCII.GetBytes(contentStream);
        StartObject(8);
        WriteRaw($"<< /Length {contentBytes.Length} >>\nstream\n");
        _output.Write(contentBytes, 0, contentBytes.Length);
        WriteRaw("\nendstream\nendobj\n");

        // XRef Table
        long xrefOffset = _output.Position;
        WriteRaw($"xref\n0 {_objectOffsets.Count + 1}\n");
        WriteRaw("0000000000 65535 f \n");
        foreach (long offset in _objectOffsets)
        {
            WriteRaw($"{offset:D10} 00000 n \n");
        }

        // Trailer
        WriteRaw($"trailer\n<< /Size {_objectOffsets.Count + 1} /Root 1 0 R >>\n");
        WriteRaw($"startxref\n{xrefOffset}\n%%EOF\n");

        return _output.ToArray();
    }

    private void StartObject(int objectNumber)
    {
        _objectOffsets.Add(_output.Position);
        WriteRaw($"{objectNumber} 0 obj\n");
    }

    private void WriteRaw(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        _output.Write(bytes, 0, bytes.Length);
    }
}
