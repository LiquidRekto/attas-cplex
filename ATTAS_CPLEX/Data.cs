using ILOG.Concert;
using ILOG.CPLEX;


namespace ATTAS_CPLEX
{
    public class Data
    {
        bool initialized = false;


        // Static vars
        /*
        ################################
        ||           Option           ||
        ################################
        */


        public double maxSearchingTimeOption { get; set; } = 30.0;
        public int strategyOption { get; set; } = 2;

        public int[] objOption { get; set; } = new int[6] { 0, 1, 1, 0, 1, 1 };
        public int[] objWeight { get; set; } = new int[6] { 1, 1, 1, 1, 1, 1 };

        /*
        ################################
        ||           DATA             ||
        ################################
        */

        public int Lecturers { get; set; } = 0;             // L
        public int Courses { get; set; } = 0;               // C
        public int Rooms { get; set; } = 0;                 // R
        public int Slots { get; set; } = 0;                 // S

        public int Days { get; set; } = 0;
        public int Times { get; set; } = 0;
        public int Segments { get; set; } = 0;
        public int SlotSegmentRules { get; set; } = 0;
        public int Tasks { get; set; } = 0;                  // T
        public int BackupLecturers { get; set; } = 0;

        // Matrix data
        public int[,]? LecturerCourseAvailability { get; set; } = new int[0, 0];               // P
        public int[,]? LecturerSlotAvailability { get; set; } = new int[0, 0];                 // F
        public int[,]? LecturerSlotPreference { get; set; } = new int[0, 0];                   // M
        public int[,]? LecturerCoursePreference { get; set; } = new int[0, 0];                 // N
        public int[,]? SlotDay { get; set; } = new int[0, 0];
        public int[,]? SlotTime { get; set; } = new int[0, 0];
        public int[,,]? SlotSegments { get; set; } = new int[0, 0, 0];

        public int[] PatternCost { get; set; } = Array.Empty<int>();
        public int[,]? AreaDistance { get; set; } = new int[0, 0];                             // E
        public int[,]? AreaSlotCoefficient { get; set; } = new int[0, 0];                           // Q
        public int[,]? SlotConflict { get; set; } = new int[0, 0];                             // D
        public int[]? LecturerQuota { get; set; } = new int[0];                                // K

        public int[]? LecturerMinQuota { get; set; } = new int[0];                                // K

        // Mappings
        public int[]? TaskCourseMapping { get; set; } = Array.Empty<int>();
        public int[]? TaskSlotMapping { get; set; } = Array.Empty<int>();
        public int[]? TaskAreaMapping { get; set; } = Array.Empty<int>();

        

        


        public Dictionary<(int, int), IIntVar>? LecturerCourseStatus;

        public List<(int, int, int)> LecturerPreassign { get; set; } = new List<(int, int, int)>();

        /*
        ################################
        ||           MODEL            ||
        ################################
        */

        public void ImportMatrixData(int[,] targetVar, params int[] data)
        {
            //Console.WriteLine("Row Length: {0}", targetVar.GetLength(0));
            //Console.WriteLine("Col Length: {0}", targetVar.GetLength(1));
            if (data.Length < targetVar.GetLength(0) * targetVar.GetLength(1))
            {
                throw new System.Exception("Input data size mismatch! Required " + targetVar.GetLength(0) * targetVar.GetLength(1) + " inputs but you only input " + data.Length);
            }
            for (int i = 0; i < targetVar.GetLength(0); i++)
            {
                for (int k = 0; k < targetVar.GetLength(1); k++)
                {
                    //Console.WriteLine("Imported: {0} to row {1}, col {2} - data pos: {3}", data[i * targetVar.GetLength(1) + k], i, k, i * targetVar.GetLength(1) + k);
                    targetVar[i, k] = data[i * targetVar.GetLength(1) + k];
                }
            }
        }

        public void ImportArrayData(int[] targetVar, params int[] data)
        {
            if (data.Length < targetVar.Length)
            {
                throw new System.Exception("Input data size mismatch! Required " + targetVar.GetLength(0) * targetVar.GetLength(1) + " inputs but you only input " + data.Length);
            }
            for (int i = 0; i < targetVar.Length; i++)
            {
                targetVar[i] = data[i];
            }
        }


    }
}