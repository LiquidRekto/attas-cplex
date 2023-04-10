using ILOG.Concert;
using ILOG.CPLEX;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Reflection.Metadata.BlobBuilder;

namespace ATTAS_CPLEX
{
    public class Model
    {
        private void createHintValues(Cplex cp, IIntVar[] vars, int hintTarget, string hintName)
        {
            double[] values = new double[vars.Length];
            Array.Clear(values, hintTarget, values.Length);
            Cplex.MIPStartEffort effort = Cplex.MIPStartEffort.Auto;
            string name = hintName;
            cp.AddMIPStart(vars, values, effort);
        }

        /*
        private INumExpr getPiecewiseSqrt(Cplex cp, INumExpr expr, int maxVal)
        {
            List<double> breakpoints = new List<double>();
            List<double> slopes = new List<double>();
            int slInd = 0;

            for (int i = 0; i < maxVal; i++)
            {
                if (Math.Sqrt(i*i) % 1 == 0)
                {
                    breakpoints.Add(i*i);
                    if (slInd == 0)
                        slopes.Add(0.0);
                    else
                        slopes.Add((double)i / slInd);
                }

            }
            return cplex.PiecewiseLinear(expr, breakpoints.ToArray(), slopes.ToArray(), 0.0, maxVal*maxVal);
        }
        */

        private Data data;
        private Cplex cplex;

        private List<IIntVar> hintPool = new List<IIntVar>();

        public bool hasSolution = false; 

        //RANGE
        private int[] AllCourses = Array.Empty<int>();
        private int[] AllTasks = Array.Empty<int>();
        private int[] AllSlots = Array.Empty<int>();
        private int[] AllDays = Array.Empty<int>();
        private int[] AllTimes = Array.Empty<int>();
        private int[] AllSegments = Array.Empty<int>();
        private int[] AllLecturers = Array.Empty<int>();
        private int[] AllRooms = Array.Empty<int>();
        private int[] AllLecturersWithBackup = Array.Empty<int>();

        // Desicion variable
        private Dictionary<(int, int), IIntVar> assigns; // BoolVar
        private Dictionary<(int, int), IIntVar> lecturerCourseStatus; // BoolVar
        private Dictionary<(int, int), IIntVar> lecturerDayStatus;
        private Dictionary<(int, int, int), IIntVar> lecturerTimeStatus;
        private Dictionary<(int, int, int), IIntVar> lecturerSegmentStatus;
        private Dictionary<(int, int, int), IIntVar> lecturerPatternStatus;
        private Dictionary<(int, int), INumExpr> assignsProduct;

        public void importData(Data dat)
        {
            data = dat;
        }

        internal void setSolverCount()
        {
            AllCourses = Enumerable.Range(0, data.Courses).ToArray();
            AllTasks = Enumerable.Range(0, data.Tasks).ToArray();
            AllSlots = Enumerable.Range(0, data.Slots).ToArray();
            AllDays = Enumerable.Range(0, data.Days).ToArray();
            AllTimes = Enumerable.Range(0, data.Times).ToArray();
            AllSegments = Enumerable.Range(0, data.Segments).ToArray();
            AllLecturers = Enumerable.Range(0, data.Lecturers).ToArray();
            AllRooms = Enumerable.Range(0, data.Rooms).ToArray();

            if (data.BackupLecturers > 0)
            {
                AllLecturersWithBackup = Enumerable.Range(0, data.Lecturers + 1).ToArray();
                data.LecturerQuota = data.LecturerQuota.Concat(new int[] { data.BackupLecturers }).ToArray();
                data.LecturerMinQuota = data.LecturerMinQuota.Concat(new int[] { data.BackupLecturers }).ToArray();
            }
            else
            {
                AllLecturersWithBackup = Enumerable.Range(0, data.Lecturers).ToArray();
            }
        }

        internal void createModel()
        {
            cplex = new Cplex();

            assigns = new Dictionary<(int, int), IIntVar>();
            foreach (int n in AllTasks)
                foreach (int i in AllLecturersWithBackup)
                {
                    assigns.Add((n, i), cplex.BoolVar($"n{n}i{i}"));
                }

            List<INumExpr> literals = new List<INumExpr>();
            //C-00 EACH TASK ASSIGN TO ATLEAST ONE AND ONLY ONE
            foreach (int n in AllTasks)
            {
                foreach (int i in AllLecturersWithBackup)
                    literals.Add(assigns[(n, i)]);
                cplex.AddEq(cplex.Sum(literals.ToArray()), 1); // EXACTLY ONE
                literals.Clear();
            }

            //C-00 CONSTRAINT INSTRUCTOR QUOTA MUST IN RANGE
            List<IIntVar> taskAssigned = new List<IIntVar>();
            foreach (int i in AllLecturersWithBackup)
            {
                foreach (int n in AllTasks)
                    taskAssigned.Add(assigns[(n, i)]);
                cplex.AddRange(data.LecturerMinQuota[i], cplex.Sum(taskAssigned.ToArray()), data.LecturerQuota[i]);
                taskAssigned.Clear();
            }
            
            List<List<int>> task_in_this_slot = new List<List<int>>();
            List<List<int>> task_conflict_with_this_slot = new List<List<int>>();

            foreach (int s in AllSlots)
            {
                List<int> sublist_task_in_this_slot = new List<int>();
                List<int> sublist_task_conflict_with_this_slot = new List<int>();

                foreach (int n in AllTasks)
                {
                    if (data.TaskSlotMapping[n] == s)
                        sublist_task_in_this_slot.Add(n);

                    if (data.SlotConflict[data.TaskSlotMapping[n], s] == 1)
                        sublist_task_conflict_with_this_slot.Add(n);
                }
                task_in_this_slot.Add(sublist_task_in_this_slot);
                task_conflict_with_this_slot.Add(sublist_task_conflict_with_this_slot);
            }
            
            //C-01 NO SLOT CONFLICT
            List<INumExpr> taskAssignedThatSlot = new List<INumExpr>();
            List<INumExpr> taskAssignedConflictWithThatSlot = new List<INumExpr>();
            foreach (int i in AllLecturers)
                foreach (int s in AllSlots)
                {
                    foreach (int n in task_in_this_slot[s])
                        taskAssignedThatSlot.Add(assigns[(n, i)]);
                    foreach (int n in task_conflict_with_this_slot[s])
                        taskAssignedConflictWithThatSlot.Add(assigns[(n, i)]);
                    IIntVar tmp = cplex.BoolVar("");
                    cplex.Add(cplex.IfThen(cplex.Eq(tmp, 1), cplex.Ge(cplex.Sum(taskAssignedThatSlot.ToArray()), 1)));
                    cplex.Add(cplex.IfThen(cplex.Not(cplex.Eq(tmp, 1)), cplex.Eq(cplex.Sum(taskAssignedThatSlot.ToArray()), 0)));
                    cplex.Add(cplex.IfThen(cplex.Eq(tmp, 1), cplex.Eq(cplex.Sum(taskAssignedConflictWithThatSlot.ToArray()), 1)));
                    //model.Add(LinearExpr.Sum(taskAssignedThatSlot) > 0).OnlyEnforceIf(tmp);
                    //model.Add(LinearExpr.Sum(taskAssignedThatSlot) == 0).OnlyEnforceIf(tmp.Not());
                    //model.Add(LinearExpr.Sum(taskAssignedConflictWithThatSlot) == 1).OnlyEnforceIf(tmp);
                    taskAssignedThatSlot.Clear();
                    taskAssignedConflictWithThatSlot.Clear();
                }
            
            

            //C-02 PREASSIGN MUST BE SATISFY
            foreach (var data in data.LecturerPreassign)
            {
                if (data.Item3 == 1)
                    cplex.AddEq(assigns[(data.Item2, data.Item1)], 1);
                    //model.Add(assigns[(data.Item2, data.Item1)] == 1);
                if (data.Item3 == -1)
                    cplex.AddEq(assigns[(data.Item2, data.Item1)], 0);
                    //model.Add(assigns[(data.Item2, data.Item1)] == 0);
            }

            //C-03 INSTRUCTOR MUST HAVE ABILITY FOR THAT SUBJECT
            foreach (int n in AllTasks)
                foreach (int i in AllLecturers)
                    cplex.AddGe(cplex.Abs(cplex.Sum(-data.LecturerCourseAvailability[i, data.TaskCourseMapping[n]],assigns[(n, i)])), 0);
                   // model.Add(instructorSubject[i, taskSubjectMapping[n]] - assigns[(n, i)] > -1);

            //C-04 INSTRUCTOR MUST BE ABLE TO TEACH IN THAT SLOT
            foreach (int n in AllTasks)
                foreach (int i in AllLecturers)
                    cplex.AddGe(cplex.Abs(cplex.Sum(-data.LecturerSlotAvailability[i, data.TaskSlotMapping[n]], assigns[(n, i)])), 0);
                 //model.Add(instructorSlot[i, taskSlotMapping[n]] - assigns[(n, i)] > -1);
            
        }

        /*
        ################################
        ||         OBJECTIVE          ||
        ################################
        */

        // O-01 MINIMIZE DAY
        
        public INumExpr objTeachingDay()
        {
            List<IIntVar> teachingDay = new List<IIntVar>();
            foreach (int i in AllLecturers)
                foreach (int d in AllDays)
                    if (lecturerDayStatus.TryGetValue((i, d), out IIntVar value))
                        teachingDay.Add(value);
            return cplex.Sum(teachingDay.ToArray());
        }
        // O-02 MINIMIZE TIME
        public INumExpr objTeachingTime()
        {
            List<IIntVar> teachingTime = new List<IIntVar>();
            foreach (int i in AllLecturers)
                foreach (int d in AllDays)
                    foreach (int t in AllTimes)
                        if (lecturerTimeStatus.TryGetValue((i, d, t), out IIntVar value))
                            teachingTime.Add(value);
            return cplex.Sum(teachingTime.ToArray());
        }

        // O-03 MINIMIZE SEGMENT COST
        public INumExpr objPatternCost()
        {
            List<INumExpr> allPatternCost = new List<INumExpr>();
            Console.WriteLine($"{lecturerPatternStatus.Keys}");
            foreach (int i in AllLecturers)
                foreach (int d in AllDays)
                    for (int p = 0; p < (1 << data.Segments); p++)
                    {
                        allPatternCost.Add(cplex.Prod(data.PatternCost[p], lecturerPatternStatus[(i, d, p)]));
                    }
                        
            return cplex.Sum(allPatternCost.ToArray());
        }

        // O-04 MINIMIZE SUBJECT DIVERSITY
        public INumExpr objSubjectDiversity()
        {
            List<IIntVar> literals = new List<IIntVar>();
            List<INumExpr> subjectDiversity = new List<INumExpr>();
            foreach (int l in AllLecturers)
            {
                foreach (int c in AllCourses)
                    literals.Add(lecturerCourseStatus[(l, c)]);
                subjectDiversity.Add(cplex.Sum(literals.ToArray()));
                literals.Clear();
            }
            IIntVar obj = cplex.IntVar(0, data.Courses, "courseDiversity");
            cplex.Add(cplex.Eq(obj, cplex.Max(subjectDiversity.ToArray())));
           // model.AddMaxEquality(obj, subjectDiversity);
            return obj;
        }
        // O-05 MINIMIZE QUOTA DIFF
        public INumExpr objQuotaReached()
        {
            List<INumExpr> quotaDifference = new List<INumExpr>();
            foreach (int l in AllLecturers)
            {
                IIntVar[] x = new IIntVar[data.Tasks];
                foreach (int n in AllTasks)
                    x[n] = assigns[(n, l)];
                quotaDifference.Add(cplex.Sum(-data.LecturerQuota[l],cplex.Sum(x)));
            }
            IIntVar obj = cplex.IntVar(0, data.Tasks, "maxQuotaDifference");
            cplex.Add(cplex.Eq(obj, cplex.Max(quotaDifference.ToArray())));
            // model.AddMaxEquality(obj, quotaDifference);
            return obj;
        }

        // O-06 MINIMIZE WALKING DISTANCE
        public INumExpr objWalkingDistance()
        {
            List<INumExpr> walkingDistance = new List<INumExpr>();
            for (int n1 = 0; n1 < data.Tasks - 1; n1++)
                for (int n2 = n1 + 1; n2 < data.Tasks; n2++)
                {
                    if (data.AreaSlotCoefficient[data.TaskSlotMapping[n1], data.TaskSlotMapping[n2]] == 0 || data.AreaDistance[data.TaskAreaMapping[n1], data.TaskAreaMapping[n2]] == 0)
                        continue;
                    walkingDistance.Add(cplex.Prod(assignsProduct[(n1, n2)],data.AreaSlotCoefficient[data.TaskSlotMapping[n1], data.TaskSlotMapping[n2]] * data.AreaDistance[data.TaskAreaMapping[n1], data.TaskAreaMapping[n2]]));
                }
            return cplex.Sum(walkingDistance.ToArray());
        }

        // O-07
        public INumExpr objSubjectPreference()
        {
            //IIntVar[] assignedTasks = new IIntVar[data.Tasks * data.Lecturers];
            //int[] assignedTaskSubjectPreferences = new int[data.Tasks * data.Lecturers];
            INumExpr[] weightedExprs = new INumExpr[data.Tasks * data.Lecturers];
            foreach (int n in AllTasks)
            {
                foreach (int i in AllLecturers)
                {
                    weightedExprs[n * data.Lecturers + i] = cplex.Prod(data.LecturerCoursePreference[i, data.TaskCourseMapping[n]], assigns[(n, i)]);
                    //assignedTasks[n * data.Lecturers + i] = assigns[(n, i)];
                    //assignedTaskSubjectPreferences[n * data.Lecturers + i] = data.LecturerCoursePreference[i, data.TaskCourseMapping[n]];
                }
            }
            return cplex.Sum(weightedExprs);
        }
        // O-08
        public INumExpr objSlotPreference()
        {
            //IntVar[] assignedTasks = new IntVar[data.Tasks * data.Lecturers];
            //int[] assignedTaskSlotPreferences = new int[data.Tasks * data.Lecturers];
            INumExpr[] weightedExprs = new INumExpr[data.Tasks * data.Lecturers];
            foreach (int n in AllTasks)
            {
                foreach (int i in AllLecturers)
                {
                    weightedExprs[n * data.Lecturers + i] = cplex.Prod(data.LecturerSlotPreference[i, data.TaskSlotMapping[n]], assigns[(n, i)]);
                    // assignedTasks[n * numInstructors + i] = assigns[(n, i)];
                    // assignedTaskSlotPreferences[n * numInstructors + i] = instructorSlotPreference[i, taskSlotMapping[n]];
                }
            }
            return cplex.Sum(weightedExprs);
        }

        /*
        ################################
        ||         UTILITIES          ||
        ################################
        */
        public INumExpr createDelta(int maxDelta, INumExpr actualValue, int targetValue)
        {
            IIntVar delta = cplex.IntVar(0, maxDelta, "");
            cplex.AddLe(actualValue, cplex.Sum(targetValue, delta));
            cplex.AddGe(actualValue, cplex.Sum(-targetValue, delta));
            return delta;
        }
        public INumExpr createPow2(INumExpr actualValue, int targetValue)
        {
            IIntVar obj = cplex.IntVar(0, 255, "");
            
            cplex.AddEq(obj, cplex.Square(cplex.Sum(-targetValue, actualValue)));
            return obj;
        }

        public INumExpr boolState(IIntVar variable, bool state)
        {
            if (state) return cplex.Eq(variable, 1);
            else return cplex.Not(cplex.Eq(variable, 1));
        }

        public List<List<(int, int)>> getResults(Cplex cplex)
        {
            List<(int, int)> result = new List<(int, int)>();
            foreach (int n in AllTasks)
            {
                bool isAssigned = false;
                foreach (int i in AllLecturers)
                {
                    if (cplex.GetValue(assigns[(n, i)]) == 1L)
                    {
                        isAssigned = true;
                        result.Add((n, i));
                    }
                }
                if (!isAssigned)
                {
                    result.Add((n, -1));
                }
            }
            List<List<(int, int)>> results = new List<List<(int, int)>> { result };
            return results;
        }

        public List<List<(int, int)>> executeSolve()
        {
            if (cplex.Solve())
            {
                hasSolution = true;
                // 
                Console.WriteLine($"[OUTPUT] OBJECTIVE: {cplex.GetObjValue()} STATUS: {cplex.GetStatus()}");
                return getResults(cplex);
            }
            else
            {
                Console.WriteLine("[OUTPUT] NO SOLUTION!");
                return null;
            }
        }

        public List<List<(int, int)>> solveWithConstraintOnly(double maxTime)
        {
            setSolverCount();
            createModel();

            List<IIntVar> obj = new List<IIntVar>();
            foreach (int n in AllTasks)
                foreach (int i in AllLecturers)
                    obj.Add(assigns[(n, i)]);
            cplex.Minimize(createDelta(data.Tasks, cplex.Sum(obj.ToArray()), data.Tasks));

            cplex.SetParam(Cplex.Param.TimeLimit, maxTime);
            return executeSolve();
        }

        public List<List<(int, int)>> solveWithObjectives(bool[] objectiveConfig, int[] objectiveWeight, int strategyOption, double maxTime)
        {
            setSolverCount();
            createModel();
            cplex.SetParam(Cplex.Param.TimeLimit, maxTime);

            List<int> weights = new List<int>();
            List<INumExpr> totalDeltas = new List<INumExpr>();

            //O-01 MINIMIZE TEACHING DAY
            if (objectiveConfig[0])
            {
                List<IIntVar> literals = new List<IIntVar>();
                lecturerDayStatus = new Dictionary<(int, int), IIntVar>();
                foreach (int i in AllLecturers)
                    foreach (int d in AllDays)
                    {
                        foreach (int n in AllTasks)
                            if (data.LecturerSlotAvailability[i, data.TaskSlotMapping[n]] == 1 && data.LecturerSlotAvailability[i, data.TaskSlotMapping[n]] == 1 && data.SlotDay[data.TaskSlotMapping[n], d] == 1)
                                literals.Add(assigns[(n, i)]);
                        if (literals.Count() != 0)
                        {
                            lecturerDayStatus.Add((i, d), cplex.BoolVar($"i{i}d{d}"));
                            cplex.Add(cplex.IfThen(cplex.Eq(lecturerDayStatus[(i,d)],1),cplex.Ge(cplex.Sum(literals.ToArray()),1)));
                            cplex.Add(cplex.IfThen(cplex.Not(cplex.Eq(lecturerDayStatus[(i, d)], 1)), cplex.Eq(cplex.Sum(literals.ToArray()), 0)));
                        }
                        literals.Clear();
                    }

                switch (strategyOption)
                {
                    case 1:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[0],objTeachingDay()));
                        break;
                    case 2:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[0],createDelta(data.Days * data.Lecturers, objTeachingDay(), 0)));
                        break;
                    case 3:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[0],createPow2(objTeachingDay(), 0)));
                        break;
                }
            }

            // O-02 MINIMIZE TIME
            if (objectiveConfig[1])
            {
                List<IIntVar> literals = new List<IIntVar>();
                lecturerTimeStatus = new Dictionary<(int, int, int), IIntVar>();
                foreach (int i in AllLecturers)
                    foreach (int d in AllDays)
                        foreach (int t in AllTimes)
                        {
                            foreach (int n in AllTasks)
                                if (data.LecturerSlotAvailability[i, data.TaskSlotMapping[n]] == 1 && data.LecturerSlotAvailability[i, data.TaskSlotMapping[n]] == 1 && data.SlotDay[data.TaskSlotMapping[n], d] == 1 && data.SlotTime[data.TaskSlotMapping[n], t] == 1)
                                    literals.Add(assigns[(n, i)]);
                            if (literals.Count() != 0)
                            {
                                lecturerTimeStatus.Add((i, d, t), cplex.BoolVar($"i{i}d{d}s{t}"));
                                cplex.Add(cplex.IfThen(cplex.Eq(lecturerTimeStatus[(i ,d, t)], 1), cplex.Ge(cplex.Sum(literals.ToArray()), 1)));
                                cplex.Add(cplex.IfThen(cplex.Not(cplex.Eq(lecturerTimeStatus[(i, d, t)], 1)), cplex.Eq(cplex.Sum(literals.ToArray()), 0)));
                            }
                            literals.Clear();
                        }
                switch (strategyOption)
                {
                    case 1:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[1],objTeachingTime()));
                        break;
                    case 2:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[1],createDelta(data.Times * data.Days * data.Lecturers, objTeachingTime(), 0)));
                        break;
                    case 3:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[1], createPow2(objTeachingTime(), 0)));
                        break;
                }
            }

            // O-03 MINIMIZE PATTERN COST ( numInstructors * numDays * ( numSegments + 2^num Segments )
            if (objectiveConfig[2])
            {
                List<IIntVar> literals = new List<IIntVar>();
                lecturerSegmentStatus = new Dictionary<(int, int, int), IIntVar>();
                foreach (int i in AllLecturers)
                    foreach (int d in AllDays)
                        foreach (int sm in AllSegments)
                        {
                            foreach (int n in AllTasks)
                                if (data.LecturerSlotAvailability [i, data.TaskSlotMapping[n]] == 1 && data.LecturerSlotAvailability[i, data.TaskSlotMapping[n]] == 1 && data.SlotSegments[data.TaskSlotMapping[n], d, sm] == 1)
                                    literals.Add(assigns[(n, i)]);
                            lecturerSegmentStatus.Add((i, d, sm), cplex.BoolVar($"i{i}d{d}sm{sm}"));
                            if (literals.Count() == 0)
                                hintPool.Add(lecturerSegmentStatus[(i, d, sm)]);
                            /*
                            if (literals.Count() == 0) -> Hint =?????
                                model.AddHint(instructorSegmentStatus[(i, d, sm)], 0);
                            */
                            cplex.Add(cplex.IfThen(cplex.Eq(lecturerSegmentStatus[(i, d, sm)], 1), cplex.Ge(cplex.Sum(literals.ToArray()), 1)));
                            cplex.Add(cplex.IfThen(cplex.Not(cplex.Eq(lecturerSegmentStatus[(i, d, sm)], 1)), cplex.Eq(cplex.Sum(literals.ToArray()), 0)));
                            literals.Clear();
                        }
                lecturerPatternStatus = new Dictionary<(int, int, int), IIntVar>();
                List<INumExpr> exprs = new List<INumExpr>();
                foreach (int i in AllLecturers)
                    foreach (int d in AllDays)
                        for (int p = 0; p < (1 << data.Segments); p++)
                        {
                            foreach (int sm in AllSegments)
                                if ((p & (1 << (data.Segments - sm - 1))) > 0)
                                    exprs.Add(boolState(lecturerSegmentStatus[(i, d, sm)], true));
                                else
                                    exprs.Add(boolState(lecturerSegmentStatus[(i, d, sm)], false));
                            lecturerPatternStatus.Add((i, d, p), cplex.BoolVar($"i{i}d{d}p{p}"));
                            //Console.WriteLine($"Value of pattern status at pos {(i, d, p)} = {lecturerPatternStatus[(i,d,p)]}");
                            cplex.Add(cplex.IfThen(cplex.Eq(lecturerPatternStatus[(i, d, p)], 1), cplex.Eq(cplex.Sum(exprs.ToArray()), data.Segments)));
                            cplex.Add(cplex.IfThen(cplex.Not(cplex.Eq(lecturerPatternStatus[(i, d, p)], 1)), cplex.Not(cplex.Eq(cplex.Sum(exprs.ToArray()), data.Segments))));
                            exprs.Clear();
                        }
                switch (strategyOption)
                {
                    case 1:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[2],objPatternCost()));
                        break;
                    case 2:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[2],createDelta((1 << data.Segments) * data.Days * data.Lecturers * data.Segments, objPatternCost(), 0)));
                        break;
                    case 3:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[2],createPow2(objPatternCost(), 0)));
                        break;
                }
            }



            // O-04 MINIMIZE SUBJECT DIVERSITY ( numInstructor * numSubject )
            if (objectiveConfig[3])
            {
                lecturerCourseStatus = new Dictionary<(int, int), IIntVar>();
                List<IIntVar> literals = new List<IIntVar>();
                foreach (int i in AllLecturers)
                    foreach (int s in AllCourses)
                    {
                        foreach (int n in AllTasks)
                            if (data.TaskCourseMapping[n] == s)
                                literals.Add(assigns[(n, i)]);

                        
                        //if (literals.Count() == 0)
                            // model.AddHint(instructorSubjectStatus[(i, s)], 0); Hint -> ???
                        lecturerCourseStatus.Add((i, s), cplex.BoolVar($"i{i}s{s}"));
                        if (literals.Count() == 0)
                        {
                            hintPool.Add(lecturerCourseStatus[(i, s)]);
                        }
                        cplex.Add(cplex.IfThen(cplex.Eq(lecturerCourseStatus[(i, s)], 1), cplex.Ge(cplex.Sum(literals.ToArray()), 1)));
                        cplex.Add(cplex.IfThen(cplex.Not(cplex.Eq(lecturerCourseStatus[(i, s)], 1)), cplex.Eq(cplex.Sum(literals.ToArray()), 0)));
                        literals.Clear();
                    }
                switch (strategyOption)
                {
                    case 1:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[3],objSubjectDiversity()));
                        break;
                    case 2:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[3],createDelta(data.Courses, objSubjectDiversity(), 0)));
                        break;
                    case 3:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[3], createPow2(objSubjectDiversity(), 0)));
                        break;
                }
            }

            //O-05 MINIMIZE QUOTA DIFF
            if (objectiveConfig[4])
            {
                switch (strategyOption)
                {
                    case 1:
                        weights.Add(objectiveWeight[4]);
                        totalDeltas.Add(objQuotaReached());
                        break;
                    case 2:
                        weights.Add(objectiveWeight[4]);
                        totalDeltas.Add(createDelta(data.Tasks, objQuotaReached(), 0));
                        break;
                    case 3:
                        weights.Add(objectiveWeight[4]);
                        totalDeltas.Add(createPow2(objQuotaReached(), 0));
                        break;
                }
            }

            // O-06 MINIMIZE WALKING DISTANCE ( numTask^2 )
            if (objectiveConfig[5])
            {
                /*
                NEED FURTHER OPTIMIZE
                THIS OBJECTIVE REQUIRE NON LINEAR OPTIMIZE
                MODEL SPEED DEPEND ON NUMBER OF VARIABLE
                REDUCE VARIABLE WASTE BY ADDING MORE FILTER
                */
                assignsProduct = new Dictionary<(int, int), INumExpr>();
                List<INumExpr> mul = new List<INumExpr>();
                List<INumExpr> tmp = new List<INumExpr>();
                // symmetry breaking 
                try
                {
                    for (int n1 = 0; n1 < data.Tasks - 1; n1++)
                        for (int n2 = n1 + 1; n2 < data.Tasks; n2++)
                        {
                            // REDUCE MODEL VARIABLE WASTE
                            if (data.AreaSlotCoefficient[data.TaskSlotMapping[n1], data.TaskSlotMapping[n2]] == 0 || data.AreaDistance[data.TaskAreaMapping[n1], data.TaskAreaMapping[n2]] == 0)
                                continue;
                            foreach (int i in AllLecturers)
                            {
                                // REDUCE MODEL VARIABLE WASTE
                                if (data.LecturerSlotAvailability[i, data.TaskSlotMapping[n1]] == 0 || data.LecturerSlotAvailability[i, data.TaskSlotMapping[n2]] == 0 || data.LecturerCourseAvailability[i, data.TaskCourseMapping[n1]] == 0 || data.LecturerCourseAvailability[i, data.TaskCourseMapping[n2]] == 0)
                                    continue;
                                mul.Add(assigns[(n1, i)]);
                                mul.Add(assigns[(n2, i)]);
                                INumExpr product = cplex.BoolVar("");
                                cplex.AddEq(product, cplex.Prod(mul[0], mul[1]));
                                tmp.Add(product);
                                mul.Clear();
                            }
                            assignsProduct.Add((n1, n2), cplex.Sum(tmp.ToArray()));
                            tmp.Clear();
                        }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("An exception occurred: " + ex.Message + " on line " + ex.StackTrace);
                }
                switch (strategyOption)
                {
                    case 1:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[5],objWalkingDistance()));
                        break;
                    case 2:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[5],createDelta(Int32.MaxValue, objWalkingDistance(), 0)));
                        break;
                    case 3:
                        totalDeltas.Add(cplex.Prod(objectiveWeight[5],createPow2(objWalkingDistance(), 0)));
                        break;
                }
            }

            //O-07 MAXIMIZE SUBJECT PREFERENCE
            if (objectiveConfig[6])
            {
                switch (strategyOption)
                {
                    case 1:
                        weights.Add(-1 * objectiveWeight[6]);
                        totalDeltas.Add(objSubjectPreference());
                        break;
                    case 2:
                        weights.Add(objectiveWeight[6]);
                        totalDeltas.Add(createDelta(data.Tasks * 5, objSubjectPreference(), data.Tasks * 5));
                        break;
                    case 3:
                        weights.Add(objectiveWeight[6]);
                        totalDeltas.Add(createPow2(objSubjectPreference(), data.Tasks * 5));
                        break;
                }

            }
            //O-08 MAXIMIZE SLOT PREFERENCE
            if (objectiveConfig[7])
            {
                switch (strategyOption)
                {
                    case 1:
                        weights.Add(-1 * objectiveWeight[7]);
                        totalDeltas.Add(objSlotPreference());
                        break;
                    case 2:
                        weights.Add(objectiveWeight[7]);
                        totalDeltas.Add(createDelta(data.Tasks * 5, objSlotPreference(),data.Tasks * 5));
                        break;
                    case 3:
                        weights.Add(objectiveWeight[7]);
                        totalDeltas.Add(createPow2(objSlotPreference(), data.Tasks * 5));
                        break;
                }

                
            }
            // Adding all hints (initial values) to model
            Console.WriteLine($"[INFO] Detected ${hintPool.Count} hint values.");
            if (hintPool.Count > 0)
                createHintValues(cplex, hintPool.ToArray(), 0, "hints");

            // Create weighted sum
            List<INumExpr> objExprs = new List<INumExpr>();
            for (int i = 0; i < weights.Count; i++)
            {
                objExprs.Add(cplex.Prod(totalDeltas[i], weights[i]));
            }

            cplex.AddMinimize(cplex.Sum(objExprs.ToArray()));

            cplex.SetParam(Cplex.Param.RandomSeed, 500);
            return executeSolve();
        }

        
        public virtual List<List<(int, int)>> solve() {
            return null;
        }
        /*
        public void solve()
        {
            setSolverCount();
            createModel();

            cplex.SetParam(Cplex.Param.TimeLimit, 5000);

            if (cplex.Solve())
            {
                // 
                Console.WriteLine("[OUTPUT] RESULTS:");
                foreach (int n in AllTasks)
                {
                    bool isAssigned = false;
                    foreach (int i in AllLecturers)
                    {
                        if (cplex.GetValue(assigns[(n,i)]) == 1L)
                        {
                            isAssigned = true;
                            Console.Write($"{i} ");
                        }
                    }
                    if (!isAssigned)
                    {
                        Console.Write($"{-1} ");
                    }
                    
                }
            } else
            {
                Console.WriteLine("[OUTPUT] NO SOLUTION!");
            }
        }
        */
    }
}
