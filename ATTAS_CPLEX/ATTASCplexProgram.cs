using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ILOG.Concert;
using ILOG.CPLEX;

namespace ATTAS_CPLEX
{
    public static class ATTASCplexProgram
    {
        public static void Execute(Data data)
        {
            if (!data.isInitialzied())
            {
                throw new System.Exception("Data is not initialized!");
            }

            IModeler model;

            Cplex cp = new Cplex();

            // objectives
            /*
            // Objective 2
            IObjective obj2 = cp.Minimize();
            obj2.Name = "SubjectDiversity";
            obj2.Sense = ObjectiveSense.Minimize;

            List<INumExpr> weighted22 = new List<INumExpr>();
            List<INumExpr> weighted2 = new List<INumExpr>();

            for (int l = 0; l < data.Lecturers; l++)
            {
                List<INumExpr> subjDiversity = new List<INumExpr>();
                for (int c = 0; c < data.Courses; c++)
                {
                    
                    List<INumExpr> exprs = new List<INumExpr>();
                    for (int t = 0; t < data.Tasks; t++)
                    {
                        if (data.TaskCourse[t,c] == 1)
                        {
                            weighted2.Add(data.LecturerAssignment[l, t]);
                        }
                        data.LecturerCourseStatus.Add((l, c),cp.BoolVar($"l{l}c{c}"));
                        cp.IfThen(cp.AddGe(data.LecturerCourseStatus[(l, c)],1), cp.AddGe(cp.Sum(weighted2.ToArray()), 1));
                        cp.IfThen(cp.AddEq(data.LecturerCourseStatus[(l, c)], 0), cp.AddEq(cp.Sum(weighted2.ToArray()), 0));
                        exprs.Add(data.LecturerCourseStatus[(l, c)]);
                    }
                    subjDiversity.Add(cp.Sum(exprs.ToArray()));

                }
                INumVar obj = cp.NumVar(0, data.Courses, NumVarType.Int, "subjectDiversity");
                weighted22.Add(cp.AddEq(obj, cp.Max(subjDiversity.ToArray())));
            }
            obj2.Expr = cp.Sum(weighted22.ToArray());
            */
            // Objective 3
            IObjective obj3 = cp.Minimize();
            obj3.Name = "QuotaReached";
            obj3.Sense = ObjectiveSense.Minimize;

            List<INumExpr> weighted3 = new List<INumExpr>();

            for (int l = 0; l < data.Lecturers; l++)
            {
                INumExpr[] diffExpr = new INumExpr[data.Tasks];
                for (int t = 0; t < data.Tasks; t++)
                {
                    diffExpr[t] = cp.Sum(data.LecturerAssignment[l, t],-data.LecturerQuota[l]);
                }
                weighted3.Add(cp.Abs(cp.Sum(diffExpr)));
            }
            obj3.Expr = cp.Sum(weighted3.ToArray());

            // Objective 5
            IObjective obj5 = cp.Maximize();
            obj5.Name = "SubjectPreference";
            obj5.Sense = ObjectiveSense.Maximize;

            List<INumExpr> weighted5 = new List<INumExpr>();
            for (int t = 0; t < data.Tasks; t++)
            {

                // Sum section
                for (int c = 0; c < data.Courses; c++)
                {
                    for (int l = 0; l < data.Lecturers; l++)
                    {
                        weighted5.Add(cp.Prod(data.LecturerCoursePref[l, c] * data.TaskCourse[t, c], data.LecturerAssignment[l, t]));
                    }
                }

            }
            obj5.Expr = cp.Sum(weighted5.ToArray());


            // Objective 6
            IObjective obj6 = cp.Maximize();
            obj6.Name = "SlotPreference";
            obj6.Sense = ObjectiveSense.Maximize;

            List<INumExpr> weighted6 = new List<INumExpr>();
            for (int t = 0; t < data.Tasks; t++)
            {
                
                // Sum section
                for (int s = 0; s < data.Slots; s++)
                {
                    for (int l = 0; l < data.Lecturers; l++)
                    {
                        weighted6.Add(cp.Prod(data.LecturerSlotPreference[l, s] * data.TaskSlot[t,s], data.LecturerAssignment[l, t]));
                    }
                }
                
            }
            obj6.Expr = cp.Sum(weighted6.ToArray());
            // Weighted sum for objectives

            // staticLex or just combine all of them altogether????
            INumExpr[] objectivesArr = new INumExpr[]
            {
                obj3.Expr,
                obj5.Expr,
                obj6.Expr
            };

            // Adding to model
            cp.Add(cp.Maximize(cp.StaticLex(objectivesArr)));

            // subjects to (constraints)

            // Constraint 0-0
            for (int k = 0; k < data.Tasks; k++)
            {
                INumVar[] lecTask = new INumVar[data.Lecturers];
                for (int i = 0; i < data.Lecturers; i++)
                {
                    lecTask[i] = data.LecturerAssignment[i, k];
                }
                cp.AddEq(cp.Sum(lecTask),1);
            }
            
            //
            
            // Constraint 0-1
            for (int i = 0; i < data.Lecturers; i++)
            {
                INumVar[] lecTask = new INumVar[data.Tasks];
                for (int k = 0; k < data.Tasks; k++)
                {
                    lecTask[k] = data.LecturerAssignment[i, k];
                }
                cp.AddLe(cp.Sum(lecTask), data.LecturerQuota[i]);
            }

            // Constraint 1
            for (int l = 0; l < data.Lecturers; l++)
            {
                for (int j = 0; j < data.Slots; j++)
                {
                    INumExpr[] exprs = new INumExpr[data.Slots*data.Tasks];
                    // Sum area
                    for (int t = 0; t < data.Tasks; t++)
                    {
                        for (int i = 0; i < data.Slots; i++)
                        {
                            //Console.WriteLine("Now exec at row {0}, col {1}, block: {2}",t,i, t * data.Tasks + i);

                            exprs[t*data.Slots+i] = cp.Prod(data.TaskSlot[t, i] * data.SlotConflict[i, j], data.LecturerAssignment[l, t]);
                        }
                    }
                    
                    cp.AddLe(cp.Sum(exprs), 1);
                }
            }

            // Constraint 2
            for (int l = 0; l < data.Lecturers; l++)
            {
                for (int t = 0; t < data.Tasks; t++)
                {
                    cp.AddRange(0,cp.Sum(data.LecturerAssignment[l, t], -data.LecturerPreAssign[l, t]),1);
                }
            }

            // Constraint 3
            for (int l = 0; l < data.Lecturers; l++)
            {
                INumExpr[] exprs = new INumExpr[data.Courses * data.Tasks];
                // Sum section
                for (int t = 0; t < data.Tasks; t++)
                {
                    for (int c = 0; c < data.Courses; c++)
                    {
                        exprs[t * data.Courses + c] = cp.Prod((int)Math.Abs(1.0 - data.LecturerCourseAvailability[l, c]) * data.TaskCourse[t, c], data.LecturerAssignment[l, t]);
                    }
                }
                cp.AddEq(cp.Sum(exprs),0);
            }

            // Constraint 4
            for (int l = 0; l < data.Lecturers; l++)
            {
                INumExpr[] exprs = new INumExpr[data.Slots * data.Tasks];
                // Sum section
                for (int t = 0; t < data.Tasks; t++)
                {
                    for (int s = 0; s < data.Slots; s++)
                    {
                        exprs[t * data.Slots + s] = cp.Prod((int)Math.Abs(1.0 - data.LecturerSlotAvailability[l, s]) * data.TaskSlot[t, s], data.LecturerAssignment[l, t]);
                    }
                }
                cp.AddEq(cp.Sum(exprs), 0);
            }

            if (cp.Solve())
            {
                Console.WriteLine("Objective: {0}", cp.GetObjValue());
                for (int i = 0; i < data.LecturerAssignment.GetLength(0); i++)
                {
                    for (int k = 0; k < data.LecturerAssignment.GetLength(1); k++)
                    {
                        if (cp.GetValue(data.LecturerAssignment[i, k]) == 1) {
                            Console.WriteLine("Task {0} assigned to instructor {1}",k,i);
                        }
                        
                    }
                   // Console.WriteLine();
                }
            }
        }
    }
}
