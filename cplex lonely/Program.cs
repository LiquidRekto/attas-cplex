using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ATTAS_CPLEX;
using Microsoft.Office.Interop.Excel;
using Range = Microsoft.Office.Interop.Excel.Range;
using System.Numerics;
using ILOG.Concert;
using System.Data;

namespace cplex_lonely
{
    internal class Program
    {
        //Cplex cplex = new Cplex();

        private static string objCfgToStringBin(bool[] objCfg)
        {
            string output = "";
            foreach (bool i in objCfg)
            {
                output += i ? "1" : "0";
            }
            return output;
        }

        private static int extractInt(Worksheet ws, int cellRow, int cellCol)
        {
            int output = -999;
            try
            {
                object raw = ((Range)ws.Cells[cellRow, cellCol]).Value2;
                if (raw != null)
                {
                    output = (int)(double)raw;
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"[ERROR] {e.Message}");
            }

            return output;
            
        }

        private static string extractString(Worksheet ws, int cellRow, int cellCol)
        {
            string output = "";
            try
            {
                object raw = ((Range)ws.Cells[cellRow, cellCol]).Value2;
                if (raw != null)
                {
                    output = (string)raw;
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"[ERROR] {e.Message}");
            }

            return output;

        }

        private static void printMat(int[,] matrix)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    Console.Write(matrix[i, j] + " ");

                }
                Console.WriteLine();
            }
        }

        static void fullBorder(Range range)
        {
            // Set the border style, weight, and color
            XlLineStyle lineStyle = XlLineStyle.xlContinuous;
            XlBorderWeight lineWeight = XlBorderWeight.xlThin;
            object lineColor = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.Black);

            // Add the border to the top edge of the range
            Border topBorder = range.Borders[XlBordersIndex.xlEdgeTop];
            topBorder.LineStyle = lineStyle;
            topBorder.Weight = lineWeight;
            topBorder.Color = lineColor;

            // Add the border to the bottom edge of the range
            Border bottomBorder = range.Borders[XlBordersIndex.xlEdgeBottom];
            bottomBorder.LineStyle = lineStyle;
            bottomBorder.Weight = lineWeight;
            bottomBorder.Color = lineColor;

            // Add the border to the left edge of the range
            Border leftBorder = range.Borders[XlBordersIndex.xlEdgeLeft];
            leftBorder.LineStyle = lineStyle;
            leftBorder.Weight = lineWeight;
            leftBorder.Color = lineColor;

            // Add the border to the right edge of the range
            Border rightBorder = range.Borders[XlBordersIndex.xlEdgeRight];
            rightBorder.LineStyle = lineStyle;
            rightBorder.Weight = lineWeight;
            rightBorder.Color = lineColor;
        }

        static void alignMiddle(Range range)
        {
            range.VerticalAlignment = XlVAlign.xlVAlignCenter;
            range.HorizontalAlignment = XlHAlign.xlHAlignCenter;
        }

        static void writeOutputExcel(string outputPath, ATTASCplexProgram attas, Data data, List<List<(int, int)>>? results, ref string[] classNames, ref string[] slotNames, ref string[] instructorNames, ref string[] subjectNames)
        {
            if (results != null)
            {
                Application? oXL = null;
                Workbook? oWB = null;
                try
                {
                    DateTime currentTime = DateTime.Now;
                    string currentTimeString = currentTime.ToString("yyyy-MM-ddTHH-mm-ss");
                    oXL = new Application();
                    oWB = oXL.Workbooks.Add();
                    Worksheet oWS = (Worksheet)oWB.ActiveSheet;
                    oWS.Name = "result";
                    for (int i = 0; i < data.Lecturers; i++)
                    {
                        oWS.Cells[i + 2, 1] = instructorNames[i];
                        ((Range)oWS.Cells[i + 2, 1]).Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.DarkOrange);
                        alignMiddle((Range)oWS.Cells[i + 2, 1]);
                    }

                    oWS.Cells[data.Lecturers + 2, 1] = "UNASSIGNED";
                    ((Range)oWS.Cells[data.Lecturers + 2, 1]).Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.DarkOrange);
                    alignMiddle((Range)oWS.Cells[data.Lecturers + 2, 1]);

                    for (int i = 0; i < data.Slots; i++)
                    {
                        oWS.Cells[1, i + 2] = slotNames[i];
                        ((Range)oWS.Cells[1, i + 2]).Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.SteelBlue);
                        alignMiddle((Range)oWS.Cells[1, i + 2]);
                    }

                    for (int i = 0; i <= data.Lecturers + 1; i++)
                        for (int j = 0; j <= data.Slots; j++)
                        {
                            fullBorder((Range)oWS.Cells[i + 1, j + 1]);
                        }
                    List<(int, int)> tmp = results[0];
                    foreach ((int, int) result in tmp)
                        if (result.Item2 >= 0)
                        {
                            oWS.Cells[result.Item2 + 2, data.TaskSlotMapping[result.Item1] + 2] = $"{result.Item1 + 1}.{classNames[result.Item1]}.{subjectNames[data.TaskCourseMapping[result.Item1]]}";
                        }
                        else
                        {
                            oWS.Cells[data.Lecturers + 2, data.TaskSlotMapping[result.Item1] + 2] = ((Range)oWS.Cells[data.Lecturers + 2, data.TaskSlotMapping[result.Item1] + 2]).Value + $"{result.Item1 + 1}.{classNames[result.Item1]}.{subjectNames[data.TaskCourseMapping[result.Item1]]}\n";
                        }

                    oWS.Columns.AutoFit();
                    oWB.SaveAs($@"{outputPath}\result_{currentTimeString}_obj_{objCfgToStringBin(attas.objectiveConfig)}_dur_{attas.maxSearchingTime}s_CPLEX_{attas.strategyOption.ToString()}.xlsx");
                    oWB.Close();
                    oXL.Quit();
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine($"An exception occurred while writing output: {ex.ToString()}");
                    if (oWB != null)
                        oXL.DisplayAlerts = false;
                    oWB.Close();
                    if (oXL != null)
                        oXL.DisplayAlerts = true;
                    oXL.Quit();

                }
            }
            else
            {
                Console.WriteLine("No solution to export!");
            }
        }

        private static string[] extractArrayRowString(Worksheet ws, int row, int startCol, int endCol)
        {
            int length = endCol - startCol + 1;
            string[] arr = new string[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = (string)((Range)ws.Cells[row, startCol + i]).Value2;
            }
            return arr;
        }




        private static int[] extractArrayRowInt(Worksheet ws, int row, int startCol, int endCol)
        {
            int length = endCol - startCol + 1;
            int[] arr = new int[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = (int)(double)((Range)ws.Cells[row, startCol+i]).Value2;
            }
            return arr;
        }

        private static int[] makeArrayMap(Worksheet ws, int rows, int col, string[] namesArray)
        {
            int[] mapping = new int[rows];
            Range oRng;
            for (int i = 2; i <= rows + 1; i++)
            {
                oRng = (Range)ws.Cells[i, col];
                mapping[i - 2] = Array.IndexOf(namesArray, oRng.Value2);
            }
            return mapping;
        }


        private static string[] extractArrayColString (Worksheet ws, int col, int startRow, int endRow)
        {
            int length = endRow - startRow + 1;
            string[] arr = new string[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = (string)((Range)ws.Cells[startRow + i, col]).Value2;
            }
            return arr;
        }

        private static int[] extractArrayColInt(Worksheet ws, int col, int startRow, int endRow)
        {
            int length = endRow - startRow + 1;
            int[] arr = new int[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = (int)(double)((Range)ws.Cells[startRow + i, col]).Value2;
            }
            return arr;
        }

        private static int[,] extractMatrixInt(Worksheet ws, int startRow, int endRow, int startCol, int endCol)
        {
            int rows = endRow - startRow + 1;
            int columns = endCol - startCol + 1;
            int[,] mat = new int[rows, columns];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    mat[i, j] = (int)(double)((Range)ws.Cells[startRow + i, startCol + j]).Value2;
                }
            }
            return mat;
        }

        

        private static int[,] toBinaryMatrix(int[,] target)
        {
            int[,] output = target;
            for (int i = 0; i < output.GetLength(0); i++)
            {
                for (int k = 0; k < output.GetLength(1); k++)
                {
                    if (output[i,k] > 0)
                    {
                        output[i, k] = 1;
                    }
                }
            }
            return output;
        }

        public static void Main(string[] args)
        {
            Data dat = new Data();
            bool isDebug = true;
            const string inputPath = @"D:\LAB DESK\Test\ATTAS_CPLEX\cplex lonely\inputCF_SU23_NEW.xlsx";
            const string resultFolder = @"D:\LAB DESK\LecturerScheduling\results";

            string[] classNames = Array.Empty<string>(),
                slotNames = Array.Empty<string>(), 
                instructorNames = Array.Empty<string>(),
                subjectNames = Array.Empty<string>();
            try
            {
                
                /*
                ################################
                ||       READING EXCEL        ||
                ################################
                */
                Application oXL;
                Workbook oWB;
                //Start Excel and get Application object.
                oXL = new Application();
                oWB = oXL.Workbooks.Open(inputPath);

                // SHEETS
                Worksheet oWS_inputInfo = (Worksheet) oWB.Sheets[1];
                Worksheet oWS_tasks = (Worksheet)oWB.Sheets[2];
                Worksheet oWS_slotConflict = (Worksheet)oWB.Sheets[3];
                Worksheet oWS_slotDay = (Worksheet)oWB.Sheets[4];
                Worksheet oWS_slotTime = (Worksheet)oWB.Sheets[5];
                Worksheet oWS_slotSegment = (Worksheet)oWB.Sheets[6];
                Worksheet oWS_patternCost = (Worksheet)oWB.Sheets[7];
                Worksheet oWS_lecturerCourse = (Worksheet)oWB.Sheets[8];
                Worksheet oWS_lecturerSlot = (Worksheet)oWB.Sheets[9];
                Worksheet oWS_lecturerQuota = (Worksheet)oWB.Sheets[10];
                Worksheet oWS_lecturerPreassign = (Worksheet)oWB.Sheets[11];
                Worksheet oWS_areaDistance = (Worksheet)oWB.Sheets[12];
                Worksheet oWS_areaSlotCoefficient = (Worksheet)oWB.Sheets[13];



                Console.WriteLine($"ATTAS - Reading Data From Excel {inputPath}");

                if (isDebug) Console.WriteLine("[INFO] Extracting Tasks info...");
                dat.Tasks = extractInt(oWS_inputInfo, 1, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Tasks}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Lecturers info...");
                dat.Lecturers = extractInt(oWS_inputInfo, 2, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Lecturers}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Slots info..."); 
                dat.Slots = extractInt(oWS_inputInfo, 3, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Slots}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Days info...");
                dat.Days = extractInt(oWS_inputInfo, 4, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Days}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Time info...");
                dat.Times = extractInt(oWS_inputInfo, 5, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Times}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Segments info...");
                dat.Segments = extractInt(oWS_inputInfo, 6, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Segments}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Segment Rules info...");
                dat.SlotSegmentRules = extractInt(oWS_inputInfo, 7, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.SlotSegmentRules}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Courses info...");
                dat.Courses = extractInt(oWS_inputInfo, 8, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Courses}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Rooms info...");
                dat.Rooms = extractInt(oWS_inputInfo, 9, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.Rooms}");
                if (isDebug) Console.WriteLine("[INFO] Extracting Backup Lecturers info...");
                dat.BackupLecturers = extractInt(oWS_inputInfo, 10, 2);
                if (isDebug) Console.WriteLine($"[OUTPUT] RESULT: {dat.BackupLecturers}");

                if (isDebug) Console.WriteLine("[INFO] Extracting Class Names info...");
                classNames = extractArrayColString(oWS_tasks, 1, 2, dat.Tasks + 1);
                
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Names info...");
                slotNames = extractArrayColString(oWS_slotConflict, 1, 2, dat.Slots + 1);
                if (isDebug) Console.WriteLine("[INFO] Extracting Lecturer Names info...");
                instructorNames = extractArrayColString(oWS_lecturerCourse, 1, 2, dat.Lecturers + 1);
                if (isDebug) Console.WriteLine("[INFO] Extracting Subject Names info...");
                subjectNames = extractArrayRowString(oWS_lecturerCourse, 1, 2, dat.Lecturers + 1);
                // SLOT
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Conflict Matrix info...");
                dat.SlotConflict = extractMatrixInt(oWS_slotConflict, 2, dat.Slots + 1, 2, dat.Slots + 1);
                if (isDebug)
                {
                    Console.WriteLine("[OUTPUT]");
                    printMat(dat.SlotConflict);
                }
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Day Matrix info...");
                dat.SlotDay = extractMatrixInt(oWS_slotDay, 2, dat.Slots + 1, 2, dat.Days + 1);
                if (isDebug)
                {
                    Console.WriteLine("[OUTPUT]");
                    printMat(dat.SlotDay);
                }
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Time Matrix info...");
                dat.SlotTime = extractMatrixInt(oWS_slotTime, 2, dat.Slots + 1, 2, dat.Times + 1);
                if (isDebug)
                {
                    Console.WriteLine("[OUTPUT]");
                    printMat(dat.SlotDay);
                }
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Segment 3D Array info...");
                dat.SlotSegments = new int[dat.Slots, dat.Days, dat.Segments];
                for (int i = 0; i < dat.SlotSegmentRules; i++)
                {
                    int slot = Array.IndexOf(slotNames, extractString(oWS_slotSegment, i + 2, 1));
                    int day = extractInt(oWS_slotSegment, i + 2, 2) - 1;
                    int segment = extractInt(oWS_slotSegment, i + 2, 3) - 1;
                    dat.SlotSegments[slot, day, segment] = 1;
                }
                if (isDebug) Console.WriteLine("[INFO] Extracting Pattern Cost info...");
                dat.PatternCost = extractArrayColInt(oWS_patternCost, 2, 2, (1 << dat.Segments) + 1);
                // INSTRUCTOR
                if (isDebug) Console.WriteLine("[INFO] Extracting Course Preferences Matrix info...");
                dat.LecturerCoursePreference = extractMatrixInt(oWS_lecturerCourse, 2, dat.Lecturers + 1, 2, dat.Courses + 1);
                if (isDebug)
                {
                    Console.WriteLine("[OUTPUT]");
                    printMat(dat.LecturerCoursePreference);
                }
                if (isDebug) Console.WriteLine("[INFO] Converting Course Preferences Matrix to Course Availability info...");
                dat.LecturerCourseAvailability = toBinaryMatrix(dat.LecturerCoursePreference);
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Preferences Matrix info...");
                dat.LecturerSlotPreference = extractMatrixInt(oWS_lecturerSlot, 2, dat.Lecturers + 1, 2, dat.Slots + 1);
                if (isDebug) Console.WriteLine("[INFO] Converting Slot Preferences Matrix to Slot Availability info...");
                dat.LecturerSlotAvailability = toBinaryMatrix(dat.LecturerSlotPreference);
                if (isDebug) Console.WriteLine("[INFO] Extracting Slot Area Coefficient Matrix info...");
                dat.AreaSlotCoefficient = extractMatrixInt(oWS_areaSlotCoefficient, 2, dat.Slots + 1, 2, dat.Slots + 1);
                if (isDebug) Console.WriteLine("[INFO] Extracting Lecturer Quota Array info...");
                dat.LecturerQuota = extractArrayColInt(oWS_lecturerQuota, 3, 2, dat.Lecturers + 1);
                if (isDebug) Console.WriteLine("[INFO] Extracting Lecturer Min Quota Array info...");
                dat.LecturerMinQuota = extractArrayColInt(oWS_lecturerQuota, 2, 2, dat.Lecturers + 1);

                if (isDebug) Console.WriteLine("[INFO] Filling Lecturer Preassign info...");
                dat.LecturerPreassign = new List<(int, int, int)>();
                for (int i = 0; i < dat.Lecturers; i++)
                    for (int j = 0; j < dat.Slots; j++)
                    {
                        int content = extractInt(oWS_lecturerPreassign, i + 2, j + 2);
                        if (content != -999)
                        {
                            dat.LecturerPreassign.Add((i, content - 1, 1));
                        }
                    }
                //attas.instructorPreassign = new List<(int, int, int)> { (32, 0, 1), (32, 1, 1), (32, 2, 1) };

                // AREA
                if (isDebug) Console.WriteLine("[INFO] Extracting Area Distance Matrix info...");
                dat.AreaDistance = extractMatrixInt(oWS_areaDistance, 2, dat.Rooms + 1, 2, dat.Rooms + 1);

                // TASK
                if (isDebug) Console.WriteLine("[INFO] Creating Task - Course mapping info...");
                dat.TaskCourseMapping = makeArrayMap(oWS_tasks, dat.Tasks, 2, subjectNames);
                if (isDebug) Console.WriteLine("[INFO] Creating Task - Slot mapping info...");
                dat.TaskSlotMapping = makeArrayMap(oWS_tasks, dat.Tasks, 4, slotNames);
                if (isDebug) Console.WriteLine("[INFO] Creating Task - Room mapping info...");
                dat.TaskAreaMapping = new int[dat.Tasks];
                for (int i = 0; i < dat.Tasks; i++)
                    dat.TaskAreaMapping[i] = 1;


                oWB.Close();
                oXL.Quit();
                Console.WriteLine("Finished importing data!");

            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            
            // BEGIN SOLVING
            
            ATTASCplexProgram attas = new ATTASCplexProgram();
            Console.WriteLine("Importing data...");
            attas.importData(dat);
            Console.WriteLine("Begin solving problem!");

            attas.strategyOption = StrategyOption.ConstraintProgramming;
            attas.objectiveConfig = new bool[8] { true, true, true, true, true, true, true, true};
            attas.objectiveWeight = new int[8] { 60, 25, 1, 1, 1, 1, 1, 1 };
            //attas.activateFullObjective();
            attas.maxSearchingTime = 300;

            List<List<(int, int)>> results = attas.solve();
            
            if (attas.hasSolution)
            {
                Console.WriteLine($"[INFO] Writing Excel output...");
                try
                {
                    writeOutputExcel(resultFolder, attas, dat, results, ref classNames, ref slotNames, ref instructorNames, ref subjectNames);
                    Console.WriteLine($"[INFO] Successfully generated the output excel file!");
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to export to Excel file! Reason: {ex.Message}");
                }
            }

            // C1

            // Data dat = new Data(34, 14, 3, 22, 162);


            //ATTASCplexProgram.Execute(dat);
            // 
            /*
            IModeler model;

            Cplex cp = new Cplex();
            INumVar x1 = cp.NumVar(0, Int64.MaxValue, NumVarType.Int);
            INumVar x2 = cp.NumVar(0, Int64.MaxValue, NumVarType.Int);

            ILinearNumExpr linearExpr = cp.LinearNumExpr();

            // coef - var
            linearExpr.AddTerm(20, x1);
            linearExpr.AddTerm(10, x2);

            // maximze / minimzie obj
            cp.AddMaximize(linearExpr);

            // subjects to
            cp.AddLe(cp.Sum(x1, cp.Prod(2, x2)), 40);
            cp.AddGe(cp.Sum(cp.Prod(3, x1), x2), 30); 
            cp.AddGe(cp.Sum(cp.Prod(4 ,x1),cp.Prod(3,x2)),60);
            cp.AddGe(x1, 0);
            cp.AddGe(x2, 0);
;            //



            if (cp.Solve())
            {
                Console.WriteLine("Objective: {0}", cp.GetObjValue());
                Console.WriteLine("X1: {0}", cp.GetValue(x1));
                Console.WriteLine("X1: {0}", cp.GetValue(x2));
            }
            // Matrix stuff
            /*
            importer.setSheetIndex(0);
            dat.LecturerCoursePref = importer.getIntMatrix(2, 2 + dat.Lecturers - 1, 2, 2 + dat.Courses - 1);
            */
        }
    }
}
