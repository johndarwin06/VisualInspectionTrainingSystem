#region Namespaces

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using VisualInspectionTrainingSystem.Models;
using Spreadsheet = DocumentFormat.OpenXml.Spreadsheet;

#endregion

namespace VisualInspectionTrainingSystem.Services
{
    /// <summary>
    /// Generates CSV, Office Open XML, and PDF files from complete report snapshots.
    /// </summary>
    public sealed class ReportExportService : IReportExportService
    {
        #region Constants

        private const string ReportTitle = "Visual Inspection Training Report";
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const double PdfMargin = 24d;
        private const double PdfRowHeight = 15d;
        private const int FirstPdfPageCapacity = 23;
        private const int FollowingPdfPageCapacity = 32;

        #endregion

        #region Public Methods

        /// <summary>
        /// Writes a complete UTF-8 CSV report with metadata, summary, and rows.
        /// </summary>
        public void ExportCsv(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken)
        {
            ValidateExport(snapshot, filePath);

            WriteThroughTemporaryFile(
                filePath,
                cancellationToken,
                temporaryPath => WriteCsv(
                    snapshot,
                    temporaryPath,
                    cancellationToken));
        }

        /// <summary>
        /// Writes a complete Office Open XML workbook with typed date and numeric cells.
        /// </summary>
        public void ExportExcel(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken)
        {
            ValidateExport(snapshot, filePath);

            WriteThroughTemporaryFile(
                filePath,
                cancellationToken,
                temporaryPath => WriteExcel(
                    snapshot,
                    temporaryPath,
                    cancellationToken));
        }

        /// <summary>
        /// Writes a complete, multipage PDF with repeated table headers.
        /// </summary>
        public void ExportPdf(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken)
        {
            ValidateExport(snapshot, filePath);

            WriteThroughTemporaryFile(
                filePath,
                cancellationToken,
                temporaryPath => WritePdf(
                    snapshot,
                    temporaryPath,
                    cancellationToken));
        }

        #endregion

        #region CSV Generation

        /// <summary>
        /// Writes the structured CSV content to a temporary destination.
        /// </summary>
        private static void WriteCsv(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken)
        {
            using (StreamWriter writer = new StreamWriter(
                filePath,
                false,
                new UTF8Encoding(true)))
            {
                WriteCsvPair(writer, "Report Title", ReportTitle);
                WriteCsvPair(writer, "Report Type", snapshot.Period.ReportTypeText);
                WriteCsvPair(writer, "Selected Date Range", snapshot.Period.DateRangeText);
                WriteCsvPair(
                    writer,
                    "Generated",
                    snapshot.GeneratedAtLocal.ToString(
                        DateTimeFormat,
                        CultureInfo.InvariantCulture));
                writer.WriteLine();
                writer.WriteLine("Summary");

                foreach (KeyValuePair<string, string> value in GetSummaryText(snapshot.Summary))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteCsvPair(writer, value.Key, value.Value);
                }

                writer.WriteLine();
                writer.WriteLine(
                    "Session ID,Employee Number,Full Name,Department,Start Time,End Time," +
                    "Status,Total Questions,Correct,Wrong,Pending,Reviewed,Accuracy");

                foreach (ReportSessionRow session in snapshot.Sessions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    writer.WriteLine(string.Join(
                        ",",
                        EscapeCsv(session.SessionID.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(session.EmployeeNo),
                        EscapeCsv(session.FullName),
                        EscapeCsv(session.Department),
                        EscapeCsv(FormatDateTime(session.StartTime)),
                        EscapeCsv(FormatNullableDateTime(session.EndTime)),
                        EscapeCsv(session.Status),
                        EscapeCsv(session.TotalQuestions.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(session.CorrectAnswers.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(session.WrongAnswers.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(session.PendingAnswers.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(session.ReviewedAnswers.ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(FormatAccuracy(session.ReviewedAccuracy))));
                }
            }
        }

        /// <summary>
        /// Writes one escaped CSV key/value row.
        /// </summary>
        private static void WriteCsvPair(
            TextWriter writer,
            string key,
            string value)
        {
            writer.WriteLine(EscapeCsv(key) + "," + EscapeCsv(value));
        }

        /// <summary>
        /// Escapes one value according to RFC-compatible CSV quoting rules.
        /// </summary>
        private static string EscapeCsv(string value)
        {
            string safeValue = value ?? string.Empty;

            if (safeValue.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + safeValue.Replace("\"", "\"\"") + "\"";
            }

            return safeValue;
        }

        #endregion

        #region Excel Generation

        /// <summary>
        /// Writes a three-sheet Office Open XML workbook.
        /// </summary>
        private static void WriteExcel(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(
                filePath,
                SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Spreadsheet.Workbook();

                WorkbookStylesPart stylesPart =
                    workbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = CreateWorkbookStyles();
                stylesPart.Stylesheet.Save();

                Spreadsheet.Sheets sheets =
                    workbookPart.Workbook.AppendChild(new Spreadsheet.Sheets());

                AddInformationWorksheet(
                    workbookPart,
                    sheets,
                    snapshot,
                    cancellationToken);
                AddSummaryWorksheet(
                    workbookPart,
                    sheets,
                    snapshot.Summary,
                    cancellationToken);
                AddSessionsWorksheet(
                    workbookPart,
                    sheets,
                    snapshot.Sessions,
                    cancellationToken);

                workbookPart.Workbook.Save();
            }
        }

        /// <summary>
        /// Adds report metadata to the workbook.
        /// </summary>
        private static void AddInformationWorksheet(
            WorkbookPart workbookPart,
            Spreadsheet.Sheets sheets,
            ReportSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            WorksheetPart worksheetPart =
                workbookPart.AddNewPart<WorksheetPart>();
            Spreadsheet.SheetData data = new Spreadsheet.SheetData();

            worksheetPart.Worksheet = new Spreadsheet.Worksheet(
                CreateColumns(24d, 48d),
                data);

            cancellationToken.ThrowIfCancellationRequested();
            data.Append(CreateTextRow(1U, "Report Title", ReportTitle, 6U));
            data.Append(CreateTextRow(2U, "Report Type", snapshot.Period.ReportTypeText, 0U));
            data.Append(CreateTextRow(3U, "Selected Date Range", snapshot.Period.DateRangeText, 0U));
            data.Append(CreateTextRow(
                4U,
                "Generated",
                FormatDateTime(snapshot.GeneratedAtLocal),
                0U));

            AppendSheet(
                workbookPart,
                sheets,
                worksheetPart,
                1U,
                "Report Information");
        }

        /// <summary>
        /// Adds the complete summary section to the workbook.
        /// </summary>
        private static void AddSummaryWorksheet(
            WorkbookPart workbookPart,
            Spreadsheet.Sheets sheets,
            ReportSummary summary,
            CancellationToken cancellationToken)
        {
            WorksheetPart worksheetPart =
                workbookPart.AddNewPart<WorksheetPart>();
            Spreadsheet.SheetData data = new Spreadsheet.SheetData();

            worksheetPart.Worksheet = new Spreadsheet.Worksheet(
                CreateColumns(30d, 24d),
                data);

            uint rowIndex = 1U;
            data.Append(CreateTextRow(rowIndex++, "Metric", "Value", 2U));

            foreach (KeyValuePair<string, object> value in GetSummaryValues(summary))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Spreadsheet.Row row = new Spreadsheet.Row { RowIndex = rowIndex++ };
                row.Append(CreateInlineTextCell(value.Key, 1U));

                if (value.Value is int)
                {
                    row.Append(CreateNumberCell((int)value.Value, 5U));
                }
                else if (value.Value is DateTime)
                {
                    row.Append(CreateDateCell((DateTime)value.Value));
                }
                else if (value.Key == "Reviewed Accuracy" && value.Value is decimal)
                {
                    row.Append(CreatePercentageCell((decimal)value.Value));
                }
                else
                {
                    row.Append(CreateInlineTextCell(value.Value.ToString(), 0U));
                }

                data.Append(row);
            }

            AppendSheet(
                workbookPart,
                sheets,
                worksheetPart,
                2U,
                "Summary");
        }

        /// <summary>
        /// Adds the complete session table with typed values and a frozen header.
        /// </summary>
        private static void AddSessionsWorksheet(
            WorkbookPart workbookPart,
            Spreadsheet.Sheets sheets,
            IList<ReportSessionRow> sessions,
            CancellationToken cancellationToken)
        {
            WorksheetPart worksheetPart =
                workbookPart.AddNewPart<WorksheetPart>();
            Spreadsheet.SheetData data = new Spreadsheet.SheetData();
            Spreadsheet.SheetView sheetView = new Spreadsheet.SheetView
            {
                WorkbookViewId = 0U
            };

            sheetView.Append(new Spreadsheet.Pane
            {
                VerticalSplit = 1D,
                TopLeftCell = "A2",
                ActivePane = Spreadsheet.PaneValues.BottomLeft,
                State = Spreadsheet.PaneStateValues.Frozen
            });

            Spreadsheet.Worksheet worksheet = new Spreadsheet.Worksheet();
            worksheet.Append(new Spreadsheet.SheetViews(sheetView));
            worksheet.Append(CreateSessionColumns());
            worksheet.Append(data);
            worksheetPart.Worksheet = worksheet;

            string[] headers =
            {
                "Session ID",
                "Employee Number",
                "Full Name",
                "Department",
                "Start Time",
                "End Time",
                "Status",
                "Total Questions",
                "Correct",
                "Wrong",
                "Pending",
                "Reviewed",
                "Accuracy"
            };

            Spreadsheet.Row headerRow = new Spreadsheet.Row { RowIndex = 1U };

            foreach (string header in headers)
            {
                headerRow.Append(CreateInlineTextCell(header, 2U));
            }

            data.Append(headerRow);
            uint rowIndex = 2U;

            foreach (ReportSessionRow session in sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Spreadsheet.Row row = new Spreadsheet.Row { RowIndex = rowIndex++ };
                row.Append(CreateNumberCell(session.SessionID, 5U));
                row.Append(CreateInlineTextCell(session.EmployeeNo, 0U));
                row.Append(CreateInlineTextCell(session.FullName, 0U));
                row.Append(CreateInlineTextCell(session.Department, 0U));
                row.Append(CreateDateCell(session.StartTime));
                row.Append(session.EndTime.HasValue
                    ? CreateDateCell(session.EndTime.Value)
                    : CreateInlineTextCell("N/A", 0U));
                row.Append(CreateInlineTextCell(session.Status, 0U));
                row.Append(CreateNumberCell(session.TotalQuestions, 5U));
                row.Append(CreateNumberCell(session.CorrectAnswers, 5U));
                row.Append(CreateNumberCell(session.WrongAnswers, 5U));
                row.Append(CreateNumberCell(session.PendingAnswers, 5U));
                row.Append(CreateNumberCell(session.ReviewedAnswers, 5U));
                row.Append(session.ReviewedAccuracy.HasValue
                    ? CreatePercentageCell(session.ReviewedAccuracy.Value)
                    : CreateInlineTextCell("N/A", 0U));
                data.Append(row);
            }

            AppendSheet(
                workbookPart,
                sheets,
                worksheetPart,
                3U,
                "Sessions");
        }

        /// <summary>
        /// Appends one workbook sheet relationship.
        /// </summary>
        private static void AppendSheet(
            WorkbookPart workbookPart,
            Spreadsheet.Sheets sheets,
            WorksheetPart worksheetPart,
            uint sheetId,
            string name)
        {
            sheets.Append(new Spreadsheet.Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = sheetId,
                Name = name
            });
        }

        /// <summary>
        /// Creates the workbook styles used by all sheets.
        /// </summary>
        private static Spreadsheet.Stylesheet CreateWorkbookStyles()
        {
            Spreadsheet.Fonts fonts = new Spreadsheet.Fonts(
                new Spreadsheet.Font(
                    new Spreadsheet.FontSize { Val = 11D },
                    new Spreadsheet.FontName { Val = "Calibri" }),
                new Spreadsheet.Font(
                    new Spreadsheet.Bold(),
                    new Spreadsheet.FontSize { Val = 11D },
                    new Spreadsheet.FontName { Val = "Calibri" }),
                new Spreadsheet.Font(
                    new Spreadsheet.Bold(),
                    new Spreadsheet.FontSize { Val = 11D },
                    new Spreadsheet.Color { Rgb = "FFFFFFFF" },
                    new Spreadsheet.FontName { Val = "Calibri" }),
                new Spreadsheet.Font(
                    new Spreadsheet.Bold(),
                    new Spreadsheet.FontSize { Val = 16D },
                    new Spreadsheet.FontName { Val = "Calibri" }));

            Spreadsheet.Fills fills = new Spreadsheet.Fills(
                new Spreadsheet.Fill(
                    new Spreadsheet.PatternFill
                    {
                        PatternType = Spreadsheet.PatternValues.None
                    }),
                new Spreadsheet.Fill(
                    new Spreadsheet.PatternFill
                    {
                        PatternType = Spreadsheet.PatternValues.Gray125
                    }),
                new Spreadsheet.Fill(
                    new Spreadsheet.PatternFill(
                        new Spreadsheet.ForegroundColor { Rgb = "FF1F4E78" })
                    {
                        PatternType = Spreadsheet.PatternValues.Solid
                    }));

            Spreadsheet.Border thinBorder = new Spreadsheet.Border(
                new Spreadsheet.LeftBorder
                {
                    Style = Spreadsheet.BorderStyleValues.Thin
                },
                new Spreadsheet.RightBorder
                {
                    Style = Spreadsheet.BorderStyleValues.Thin
                },
                new Spreadsheet.TopBorder
                {
                    Style = Spreadsheet.BorderStyleValues.Thin
                },
                new Spreadsheet.BottomBorder
                {
                    Style = Spreadsheet.BorderStyleValues.Thin
                },
                new Spreadsheet.DiagonalBorder());

            Spreadsheet.Borders borders = new Spreadsheet.Borders(
                new Spreadsheet.Border(),
                thinBorder);

            Spreadsheet.NumberingFormats numberingFormats =
                new Spreadsheet.NumberingFormats(
                    new Spreadsheet.NumberingFormat
                    {
                        NumberFormatId = 164U,
                        FormatCode = "yyyy-mm-dd hh:mm:ss"
                    },
                    new Spreadsheet.NumberingFormat
                    {
                        NumberFormatId = 165U,
                        FormatCode = "0.00%"
                    });

            Spreadsheet.CellFormats cellFormats = new Spreadsheet.CellFormats(
                new Spreadsheet.CellFormat(),
                new Spreadsheet.CellFormat
                {
                    FontId = 1U,
                    ApplyFont = true
                },
                new Spreadsheet.CellFormat
                {
                    FontId = 2U,
                    FillId = 2U,
                    BorderId = 1U,
                    ApplyFont = true,
                    ApplyFill = true,
                    ApplyBorder = true,
                    Alignment = new Spreadsheet.Alignment
                    {
                        Horizontal = Spreadsheet.HorizontalAlignmentValues.Center,
                        Vertical = Spreadsheet.VerticalAlignmentValues.Center,
                        WrapText = true
                    }
                },
                new Spreadsheet.CellFormat
                {
                    NumberFormatId = 164U,
                    ApplyNumberFormat = true
                },
                new Spreadsheet.CellFormat
                {
                    NumberFormatId = 165U,
                    ApplyNumberFormat = true
                },
                new Spreadsheet.CellFormat
                {
                    NumberFormatId = 1U,
                    ApplyNumberFormat = true
                },
                new Spreadsheet.CellFormat
                {
                    FontId = 3U,
                    ApplyFont = true
                });

            return new Spreadsheet.Stylesheet(
                numberingFormats,
                fonts,
                fills,
                borders,
                new Spreadsheet.CellStyleFormats(new Spreadsheet.CellFormat()),
                cellFormats);
        }

        /// <summary>
        /// Creates a simple two-column width definition.
        /// </summary>
        private static Spreadsheet.Columns CreateColumns(
            double firstWidth,
            double secondWidth)
        {
            return new Spreadsheet.Columns(
                CreateColumn(1U, firstWidth),
                CreateColumn(2U, secondWidth));
        }

        /// <summary>
        /// Creates session-table column widths.
        /// </summary>
        private static Spreadsheet.Columns CreateSessionColumns()
        {
            double[] widths =
            {
                12d, 18d, 28d, 20d, 21d, 21d, 12d,
                15d, 10d, 10d, 10d, 11d, 12d
            };
            Spreadsheet.Columns columns = new Spreadsheet.Columns();

            for (int index = 0; index < widths.Length; index++)
            {
                columns.Append(CreateColumn((uint)index + 1U, widths[index]));
            }

            return columns;
        }

        /// <summary>
        /// Creates one custom-width worksheet column.
        /// </summary>
        private static Spreadsheet.Column CreateColumn(
            uint index,
            double width)
        {
            return new Spreadsheet.Column
            {
                Min = index,
                Max = index,
                Width = width,
                CustomWidth = true
            };
        }

        /// <summary>
        /// Creates a two-cell text row.
        /// </summary>
        private static Spreadsheet.Row CreateTextRow(
            uint rowIndex,
            string label,
            string value,
            uint valueStyle)
        {
            Spreadsheet.Row row = new Spreadsheet.Row { RowIndex = rowIndex };
            row.Append(CreateInlineTextCell(label, 1U));
            row.Append(CreateInlineTextCell(value, valueStyle));

            return row;
        }

        /// <summary>
        /// Creates one inline-string cell.
        /// </summary>
        private static Spreadsheet.Cell CreateInlineTextCell(
            string value,
            uint styleIndex)
        {
            return new Spreadsheet.Cell
            {
                DataType = Spreadsheet.CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString = new Spreadsheet.InlineString(
                    new Spreadsheet.Text(value ?? string.Empty)
                    {
                        Space = SpaceProcessingModeValues.Preserve
                    })
            };
        }

        /// <summary>
        /// Creates one integer numeric cell.
        /// </summary>
        private static Spreadsheet.Cell CreateNumberCell(
            int value,
            uint styleIndex)
        {
            return new Spreadsheet.Cell
            {
                DataType = Spreadsheet.CellValues.Number,
                StyleIndex = styleIndex,
                CellValue = new Spreadsheet.CellValue(
                    value.ToString(CultureInfo.InvariantCulture))
            };
        }

        /// <summary>
        /// Creates one real Excel serial-date cell.
        /// </summary>
        private static Spreadsheet.Cell CreateDateCell(DateTime value)
        {
            return new Spreadsheet.Cell
            {
                DataType = Spreadsheet.CellValues.Number,
                StyleIndex = 3U,
                CellValue = new Spreadsheet.CellValue(
                    value.ToOADate().ToString(CultureInfo.InvariantCulture))
            };
        }

        /// <summary>
        /// Creates one real Excel percentage cell from a percent value.
        /// </summary>
        private static Spreadsheet.Cell CreatePercentageCell(decimal value)
        {
            return new Spreadsheet.Cell
            {
                DataType = Spreadsheet.CellValues.Number,
                StyleIndex = 4U,
                CellValue = new Spreadsheet.CellValue(
                    (value / 100m).ToString(CultureInfo.InvariantCulture))
            };
        }

        #endregion

        #region PDF Generation

        /// <summary>
        /// Writes a manually paginated landscape PDF document.
        /// </summary>
        private static void WritePdf(
            ReportSnapshot snapshot,
            string filePath,
            CancellationToken cancellationToken)
        {
            using (PdfDocument document = new PdfDocument())
            {
                document.Info.Title = ReportTitle;
                int totalPages = CalculatePdfPageCount(snapshot.Sessions.Count);
                int sessionIndex = 0;

                for (int pageNumber = 1; pageNumber <= totalPages; pageNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfPage page = document.AddPage();
                    page.Size = PageSize.A4;
                    page.Orientation = PageOrientation.Landscape;

                    using (XGraphics graphics = XGraphics.FromPdfPage(page))
                    {
                        bool firstPage = pageNumber == 1;
                        double tableY = firstPage ? 172d : 50d;
                        int capacity = firstPage
                            ? FirstPdfPageCapacity
                            : FollowingPdfPageCapacity;

                        DrawPdfPageHeader(
                            graphics,
                            page,
                            snapshot,
                            pageNumber,
                            totalPages,
                            firstPage);

                        if (firstPage)
                        {
                            DrawPdfSummary(graphics, snapshot.Summary);
                        }

                        DrawPdfTableHeader(graphics, tableY);
                        double rowY = tableY + PdfRowHeight;

                        for (int row = 0;
                             row < capacity && sessionIndex < snapshot.Sessions.Count;
                             row++, sessionIndex++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            DrawPdfSessionRow(
                                graphics,
                                rowY,
                                snapshot.Sessions[sessionIndex]);
                            rowY += PdfRowHeight;
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                document.Save(filePath);
            }
        }

        /// <summary>
        /// Draws title, period, generated time, and page numbering.
        /// </summary>
        private static void DrawPdfPageHeader(
            XGraphics graphics,
            PdfPage page,
            ReportSnapshot snapshot,
            int pageNumber,
            int totalPages,
            bool includeDetails)
        {
            XFont titleFont = new XFont("Arial", 15d, XFontStyleEx.Bold);
            XFont detailFont = new XFont("Arial", 8d, XFontStyleEx.Regular);
            double width = page.Width.Point - (PdfMargin * 2d);

            graphics.DrawString(
                ReportTitle,
                titleFont,
                XBrushes.Black,
                new XRect(PdfMargin, 17d, width, 22d),
                XStringFormats.TopLeft);
            graphics.DrawString(
                "Page " + pageNumber.ToString(CultureInfo.InvariantCulture) +
                " of " + totalPages.ToString(CultureInfo.InvariantCulture),
                detailFont,
                XBrushes.Black,
                new XRect(PdfMargin, 20d, width, 16d),
                XStringFormats.TopRight);

            if (includeDetails)
            {
                graphics.DrawString(
                    "Type: " + snapshot.Period.ReportTypeText +
                    "    Period: " + snapshot.Period.DateRangeText,
                    detailFont,
                    XBrushes.Black,
                    new XRect(PdfMargin, 42d, width, 14d),
                    XStringFormats.TopLeft);
                graphics.DrawString(
                    "Generated: " + FormatDateTime(snapshot.GeneratedAtLocal),
                    detailFont,
                    XBrushes.Black,
                    new XRect(PdfMargin, 56d, width, 14d),
                    XStringFormats.TopLeft);
            }
        }

        /// <summary>
        /// Draws the required aggregate summary on the first PDF page.
        /// </summary>
        private static void DrawPdfSummary(
            XGraphics graphics,
            ReportSummary summary)
        {
            XFont labelFont = new XFont("Arial", 7.5d, XFontStyleEx.Bold);
            XFont valueFont = new XFont("Arial", 7.5d, XFontStyleEx.Regular);
            List<KeyValuePair<string, string>> values = GetSummaryText(summary);
            double columnWidth = 195d;
            double startX = PdfMargin;
            double startY = 78d;

            for (int index = 0; index < values.Count; index++)
            {
                int column = index % 4;
                int row = index / 4;
                double x = startX + (column * columnWidth);
                double y = startY + (row * 25d);

                graphics.DrawString(
                    values[index].Key,
                    labelFont,
                    XBrushes.Black,
                    new XRect(x, y, columnWidth - 8d, 12d),
                    XStringFormats.TopLeft);
                graphics.DrawString(
                    FitPdfText(
                        graphics,
                        valueFont,
                        values[index].Value,
                        columnWidth - 8d),
                    valueFont,
                    XBrushes.Black,
                    new XRect(x, y + 11d, columnWidth - 8d, 12d),
                    XStringFormats.TopLeft);
            }
        }

        /// <summary>
        /// Draws the repeated PDF session-table header.
        /// </summary>
        private static void DrawPdfTableHeader(
            XGraphics graphics,
            double y)
        {
            string[] headers =
            {
                "ID", "Employee", "Full Name", "Department", "Start", "End",
                "Status", "Total", "Correct", "Wrong", "Pending", "Reviewed", "Accuracy"
            };
            double[] widths = GetPdfColumnWidths();
            XFont font = new XFont("Arial", 6d, XFontStyleEx.Bold);
            double x = PdfMargin;

            for (int index = 0; index < headers.Length; index++)
            {
                XRect rectangle = new XRect(x, y, widths[index], PdfRowHeight);
                graphics.DrawRectangle(
                    new XPen(XColors.White, 0.5d),
                    new XSolidBrush(XColor.FromArgb(31, 78, 120)),
                    rectangle);
                graphics.DrawString(
                    headers[index],
                    font,
                    XBrushes.White,
                    rectangle,
                    XStringFormats.Center);
                x += widths[index];
            }
        }

        /// <summary>
        /// Draws one bounded, safely truncated PDF session row.
        /// </summary>
        private static void DrawPdfSessionRow(
            XGraphics graphics,
            double y,
            ReportSessionRow session)
        {
            string[] values =
            {
                session.SessionID.ToString(CultureInfo.InvariantCulture),
                session.EmployeeNo,
                session.FullName,
                session.Department,
                session.StartTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                session.EndTime.HasValue
                    ? session.EndTime.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : "N/A",
                session.Status,
                session.TotalQuestions.ToString(CultureInfo.InvariantCulture),
                session.CorrectAnswers.ToString(CultureInfo.InvariantCulture),
                session.WrongAnswers.ToString(CultureInfo.InvariantCulture),
                session.PendingAnswers.ToString(CultureInfo.InvariantCulture),
                session.ReviewedAnswers.ToString(CultureInfo.InvariantCulture),
                FormatAccuracy(session.ReviewedAccuracy)
            };
            double[] widths = GetPdfColumnWidths();
            XFont font = new XFont("Arial", 5.7d, XFontStyleEx.Regular);
            XPen border = new XPen(XColors.LightGray, 0.4d);
            double x = PdfMargin;

            for (int index = 0; index < values.Length; index++)
            {
                XRect rectangle = new XRect(x, y, widths[index], PdfRowHeight);
                graphics.DrawRectangle(border, rectangle);
                graphics.DrawString(
                    FitPdfText(
                        graphics,
                        font,
                        values[index],
                        widths[index] - 4d),
                    font,
                    XBrushes.Black,
                    new XRect(x + 2d, y, widths[index] - 4d, PdfRowHeight),
                    XStringFormats.CenterLeft);
                x += widths[index];
            }
        }

        /// <summary>
        /// Calculates a deterministic PDF page count for the fixed table layout.
        /// </summary>
        private static int CalculatePdfPageCount(int rowCount)
        {
            if (rowCount <= FirstPdfPageCapacity)
            {
                return 1;
            }

            int remaining = rowCount - FirstPdfPageCapacity;

            return 1 + (int)Math.Ceiling(
                remaining / (double)FollowingPdfPageCapacity);
        }

        /// <summary>
        /// Returns fixed landscape column widths that fit an A4 page.
        /// </summary>
        private static double[] GetPdfColumnWidths()
        {
            return new[]
            {
                28d, 48d, 92d, 65d, 73d, 73d, 43d,
                34d, 37d, 34d, 39d, 43d, 47d
            };
        }

        /// <summary>
        /// Truncates long values with an ellipsis without overflowing a PDF cell.
        /// </summary>
        private static string FitPdfText(
            XGraphics graphics,
            XFont font,
            string value,
            double maximumWidth)
        {
            string safeValue = (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");

            if (graphics.MeasureString(safeValue, font).Width <= maximumWidth)
            {
                return safeValue;
            }

            const string ellipsis = "...";
            int length = safeValue.Length;

            while (length > 0)
            {
                string candidate = safeValue.Substring(0, length) + ellipsis;

                if (graphics.MeasureString(candidate, font).Width <= maximumWidth)
                {
                    return candidate;
                }

                length--;
            }

            return ellipsis;
        }

        #endregion

        #region Shared Helpers

        /// <summary>
        /// Validates a complete export request before any destination file is touched.
        /// </summary>
        private static void ValidateExport(
            ReportSnapshot snapshot,
            string filePath)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Period == null ||
                snapshot.Summary == null ||
                snapshot.Sessions == null)
            {
                throw new ArgumentException(
                    "The report snapshot is incomplete.",
                    nameof(snapshot));
            }

            if (snapshot.IsExportLimitExceeded)
            {
                throw new InvalidOperationException(
                    "The report exceeds the export safeguard.");
            }

            if (snapshot.Sessions.Count == 0)
            {
                throw new InvalidOperationException(
                    "The report contains no session rows.");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "An export destination is required.",
                    nameof(filePath));
            }

            string directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(
                    "The export destination is unavailable.");
            }
        }

        /// <summary>
        /// Generates through a temporary file so cancellation does not leave a partial report.
        /// </summary>
        private static void WriteThroughTemporaryFile(
            string destinationPath,
            CancellationToken cancellationToken,
            Action<string> writeTemporaryFile)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            string temporaryPath = Path.Combine(
                directory,
                "." + Path.GetFileName(destinationPath) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                writeTemporaryFile(temporaryPath);
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(temporaryPath, destinationPath, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // Temporary cleanup failure must not mask the export result.
                }
            }
        }

        /// <summary>
        /// Returns all required summary values using real numeric/date values where available.
        /// </summary>
        private static List<KeyValuePair<string, object>> GetSummaryValues(
            ReportSummary summary)
        {
            return new List<KeyValuePair<string, object>>
            {
                Pair("Total Sessions", (object)summary.SessionCount),
                Pair("Completed Sessions", (object)summary.CompletedSessionCount),
                Pair("Open Sessions", (object)summary.OpenSessionCount),
                Pair("Trainees", (object)summary.TraineeCount),
                Pair("Total Questions", (object)summary.TotalQuestions),
                Pair("Reviewed Answers", (object)summary.ReviewedAnswers),
                Pair("Pending Answers", (object)summary.PendingAnswers),
                Pair("Correct Reviewed Answers", (object)summary.CorrectAnswers),
                Pair("Wrong Reviewed Answers", (object)summary.WrongAnswers),
                Pair(
                    "Reviewed Accuracy",
                    summary.AverageReviewedAccuracy.HasValue
                        ? (object)summary.AverageReviewedAccuracy.Value
                        : "N/A"),
                Pair(
                    "First Session",
                    summary.FirstSessionTime.HasValue
                        ? (object)summary.FirstSessionTime.Value
                        : "N/A"),
                Pair(
                    "Last Session",
                    summary.LastSessionTime.HasValue
                        ? (object)summary.LastSessionTime.Value
                        : "N/A")
            };
        }

        /// <summary>
        /// Returns display-ready summary values for text formats.
        /// </summary>
        private static List<KeyValuePair<string, string>> GetSummaryText(
            ReportSummary summary)
        {
            List<KeyValuePair<string, string>> values =
                new List<KeyValuePair<string, string>>();

            foreach (KeyValuePair<string, object> value in GetSummaryValues(summary))
            {
                string text;

                if (value.Value is DateTime)
                {
                    text = FormatDateTime((DateTime)value.Value);
                }
                else if (value.Key == "Reviewed Accuracy" && value.Value is decimal)
                {
                    text = FormatAccuracy((decimal)value.Value);
                }
                else
                {
                    text = Convert.ToString(
                        value.Value,
                        CultureInfo.InvariantCulture);
                }

                values.Add(new KeyValuePair<string, string>(value.Key, text));
            }

            return values;
        }

        /// <summary>
        /// Creates a key/value pair without relying on tuple language features.
        /// </summary>
        private static KeyValuePair<string, object> Pair(
            string key,
            object value)
        {
            return new KeyValuePair<string, object>(key, value);
        }

        /// <summary>
        /// Formats one reviewed-accuracy percentage or N/A.
        /// </summary>
        private static string FormatAccuracy(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) + "%"
                : "N/A";
        }

        /// <summary>
        /// Formats one local date/time consistently across text exports.
        /// </summary>
        private static string FormatDateTime(DateTime value)
        {
            return value.ToString(
                DateTimeFormat,
                CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats an optional local date/time consistently across text exports.
        /// </summary>
        private static string FormatNullableDateTime(DateTime? value)
        {
            return value.HasValue ? FormatDateTime(value.Value) : "N/A";
        }

        #endregion
    }
}
