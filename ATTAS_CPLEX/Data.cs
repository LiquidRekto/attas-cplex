using ILOG.Concert;
using ILOG.CPLEX;

namespace ATTAS_CPLEX
{
    public class Data
    {
        bool initialized = false;
        Cplex cp = new Cplex();
        // Static vars
        public int Lecturers;             // L
        public int Courses;               // C
        public int Rooms;                 // R
        public int Slots;                 // S
        public int Tasks;                 // T
        public int BackupLecturers;

        // Matrix data
        public int[,]? LecturerCourseAvailability;               // P
        public int[,]? LecturerSlotAvailability;                 // F
        public int[,]? TaskCourse;                               // X
        public int[,]? TaskSlot;                                 // Y
        public int[,]? LecturerSlotPreference;                   // M
        public int[,]? LecturerCoursePref;                       // N
        public int[,]? LecturerPreAssign;                        // O
        public int[,]? SlotCompatibility;                        // G
        public int[,]? AreaDistance;                             // E
        public int[,]? AreaSlotWeight;                           // Q
        public int[,]? SlotConflict;                             // D
        public int[]? LecturerQuota;                              // K

        
        public Dictionary<(int,int), IIntVar> LecturerCourseStatus;

        public List<(int, int, int)> LecturerPreassign { get; set; } = new List<(int, int, int)>();
        // Mappings
        public int[]? taskSubjectMapping;
        public int[]? taskSlotMapping;
        public int[]? taskAreaMapping;

       

        // Decision variables?
        public INumVar[,] LecturerAssignment;                    // A

        public Data(int Lecturers, int Courses, int Rooms, int Slots, int Tasks)
        {
            
            this.Lecturers = Lecturers;             
            this.Courses = Courses;               
            this.Rooms = Rooms;
            this.Slots = Slots;                
            this.Tasks = Tasks;

            this.InitMatricies();
        }
        
        private void InitMatricies()
        {
            LecturerCourseAvailability = new int[this.Lecturers, this.Courses];              // P
            LecturerSlotAvailability = new int[this.Lecturers, this.Slots];                // F
            TaskCourse = new int[this.Tasks, this.Courses];                              // X
            TaskSlot = new int[this.Tasks, this.Slots];                                // Y
            LecturerSlotPreference = new int[this.Lecturers, this.Slots];                  // M
            LecturerCoursePref = new int[this.Lecturers, this.Courses];                      // N
            LecturerPreAssign = new int[this.Lecturers, this.Tasks];                       // O
            SlotCompatibility = new int[this.Slots, this.Slots];                       // G
            AreaDistance = new int[this.Rooms, this.Rooms];                            // E
            AreaSlotWeight = new int[this.Slots, this.Slots];                          // Q
            SlotConflict = new int[this.Slots, this.Slots];                            // D
            LecturerQuota = new int[this.Lecturers];

            LecturerCourseStatus = new Dictionary<(int, int), IIntVar>();

        LecturerAssignment = new INumVar[this.Lecturers, this.Tasks];
            initialized= true;
        }

        public bool isInitialzied()
        {
            return initialized;
        }

        public void ImportMatrixData(int[,] targetVar,params int[] data)
        {
            //Console.WriteLine("Row Length: {0}", targetVar.GetLength(0));
            //Console.WriteLine("Col Length: {0}", targetVar.GetLength(1));
            if (data.Length < targetVar.GetLength(0)*targetVar.GetLength(1))
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

        public void FillDecisionVarMatrix()
        {
            for (int i = 0; i < this.LecturerAssignment.GetLength(0); i++)
            {
                for (int k = 0; k < this.LecturerAssignment.GetLength(1); k++)
                {
                    this.LecturerAssignment[i, k] = cp.NumVar(0, 1, NumVarType.Int);
                }
            }
        }


    }
}