﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CSparse;
using CSparse.Double.Factorization;
using CSparse.Storage;

namespace BriefFiniteElementNet
{
    /// <summary>
    /// Represents a structure which consists of nodes, elements and loads applied on its parts (parts means Nodes and Elements)
    /// </summary>
    public class Model
    {
        public Model()
        {
            this.nodes = new NodeCollection(this);
            this.elements = new ElementCollection(this);
        }

        private NodeCollection nodes;
        private ElementCollection elements;
        private StaticLinearAnalysisResult lastResult;

        /// <summary>
        /// Gets the nodes.
        /// </summary>
        /// <value>
        /// The nodes.
        /// </value>
        public NodeCollection Nodes
        {
            get { return nodes; }
        }

        /// <summary>
        /// Gets the elements.
        /// </summary>
        /// <value>
        /// The elements.
        /// </value>
        public ElementCollection Elements
        {
            get { return elements; }
        }

        /// <summary>
        /// Gets the LastResult.
        /// </summary>
        /// <value>
        /// The result of last static analysis of model.
        /// </value>
        public StaticLinearAnalysisResult LastResult
        {
            get { return lastResult; }
            private set { lastResult = value; }
        }


        /// <summary>
        /// Determines whether the specified <see cref="label"/> is valid for new <see cref="StructurePart"/> or not.
        /// </summary>
        /// <param name="label">The label.</param>
        /// <returns>yes if is valid, otherwise no</returns>
        internal bool IsValidLabel(string label)
        {
            foreach (var elm in elements)
            {
                if (FemNetStringCompairer.IsEqual(elm.Label, label))
                    return false;
            }


            foreach (var nde in nodes)
            {
                if (FemNetStringCompairer.IsEqual(nde.Label, label))
                    return false;
            }


            return true;
        }



        #region LinearSolve method and overrides

        /// <summary>
        /// Solves the instanse assuming linear behaviour (both geometric and material) for default load case.
        /// </summary>
        public void Solve()
        {
            Solve(new SolverConfiguration(LoadCase.DefaultLoadCase));
        }

        /// <summary>
        /// Solves the instanse assuming linear behaviour (both geometric and material) for specified cases.
        /// </summary>
        /// <param name="cases">The cases.</param>
        public void Solve(params LoadCase[] cases)
        {
            Solve(new SolverConfiguration(cases));
        }

        /// <summary>
        /// Solves the instanse assuming linear behaviour (both geometric and material) for specified configuration.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public void Solve(SolverConfiguration config)
        {
            TraceUtil.WritePerformanceTrace("Started solving model");

            var sp = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < nodes.Count; i++)
                nodes[i].Index = i;

            var n = nodes.Count;
            var c = 6*n;


            var maxNodePerElement = elements.Select(i => i.Nodes.Length).Max();
            var rElmMap = new int[maxNodePerElement*6];
            var kt = new CoordinateStorage<double>(c, c, 1);

            #region Determining count of fixed and free dofs

            var fixedDofCount =
                nodes.Select(
                    i =>
                        (int) i.Constraints.Dx + (int) i.Constraints.Dy + (int) i.Constraints.Dz +
                        (int) i.Constraints.Rx + (int) i.Constraints.Ry + (int) i.Constraints.Rz).Sum();

            var freeDofCount = c - fixedDofCount;

            TraceUtil.WritePerformanceTrace("Model with {0} free DoFs and {1} fixed DoFs", freeDofCount, fixedDofCount);

            #endregion

            var fMap = new int[c];
            var rMap = new int[c];
            var rrmap = new int[freeDofCount];
            var rfmap = new int[fixedDofCount];
            
            #region Assembling Kt

            foreach (var elm in elements)
            {
                var c2 = elm.Nodes.Length;

                for (var i = 0; i < c2; i++)
                {
                    rElmMap[6*i + 0] = elm.Nodes[i].Index*6 + 0;
                    rElmMap[6*i + 1] = elm.Nodes[i].Index*6 + 1;
                    rElmMap[6*i + 2] = elm.Nodes[i].Index*6 + 2;

                    rElmMap[6*i + 3] = elm.Nodes[i].Index*6 + 3;
                    rElmMap[6*i + 4] = elm.Nodes[i].Index*6 + 4;
                    rElmMap[6*i + 5] = elm.Nodes[i].Index*6 + 5;
                }

                var mtx = elm.GetGlobalStifnessMatrix();
                var d = c2*6;

                for (var i = 0; i < d; i++)
                {
                    for (var j = 0; j < d; j++)
                    {
                        kt.At(rElmMap[i], rElmMap[j], mtx[i, j]);
                    }
                }
            }

            sp.Stop();
            TraceUtil.WritePerformanceTrace("Assembling full stiffness matrix tooks about {0:#,##0} ms.",
                sp.ElapsedMilliseconds);
            sp.Restart();

            #endregion

            #region Extracting kff, kfs and kss

            var fixity = new bool[c];

            for (var i = 0; i < n; i++)
            {
                var cns = nodes[i].Constraints;

                if (cns.Dx == DofConstraint.Fixed) fixity[6*i + 0] = true;
                if (cns.Dy == DofConstraint.Fixed) fixity[6*i + 1] = true;
                if (cns.Dz == DofConstraint.Fixed) fixity[6*i + 2] = true;


                if (cns.Rx == DofConstraint.Fixed) fixity[6*i + 3] = true;
                if (cns.Ry == DofConstraint.Fixed) fixity[6*i + 4] = true;
                if (cns.Rz == DofConstraint.Fixed) fixity[6*i + 5] = true;
            }

            

            int fCnt = 0, rCnt = 0;

            /** /
            for (var i = 0; i < c; i++)
            {
                if (fixity[i])
                    fMap[i] = fCnt++;
                else
                    rMap[i] = rCnt++;
            }
            /**/

            /**/
            for (var i = 0; i < c; i++)
            {
                if (fixity[i])
                    rfmap[fMap[i] = fCnt++] = i;
                else
                    rrmap[rMap[i] = rCnt++] = i;
            }
            /**/

            var ktSparse = Converter.ToCompressedColumnStorage(kt);


            var ind = ktSparse.ColumnPointers;
            var v = ktSparse.RowIndices;
            var values = ktSparse.Values;

            var cnt = values.Count(i => i == 0.0);

            var kffCoord = new CoordinateStorage<double>(freeDofCount, freeDofCount, 128 + freeDofCount);
            var kfsCoord = new CoordinateStorage<double>(freeDofCount, fixedDofCount, 128);
            var ksfCoord = new CoordinateStorage<double>(fixedDofCount, freeDofCount, 128);
            var kssCoord = new CoordinateStorage<double>(fixedDofCount, fixedDofCount, 128);

            var cnr = 0;

            for (var i = 0; i < ind.Length - 1; i++)
            {
                var st = ind[i];
                var en = ind[i + 1];

                for (var j = st; j < en; j++)
                {
                    cnr++;
                    var row = i;
                    var col = v[j];
                    var val = values[j];

                    if (!fixity[row] && !fixity[col])
                    {
                        kffCoord.At(rMap[row], rMap[col], val);
                        continue;
                    }

                    if (!fixity[row] && fixity[col])
                    {
                        kfsCoord.At(rMap[row], fMap[col], val);
                        continue;
                    }

                    if (fixity[row] && !fixity[col])
                    {
                        ksfCoord.At(fMap[row], rMap[col], val);
                        continue;
                    }

                    if (fixity[row] && fixity[col])
                    {
                        kssCoord.At(fMap[row], fMap[col], val);
                        continue;
                    }


                    Guid.NewGuid();

                }
            }

            var tmp = kffCoord.NonZerosCount + kfsCoord.NonZerosCount + ksfCoord.NonZerosCount + kssCoord.NonZerosCount;



            sp.Stop();
            TraceUtil.WritePerformanceTrace("Extracting kff,kfs and kss from Kt matrix tooks about {0:#,##0} ms",
                sp.ElapsedMilliseconds);
            sp.Restart();

            #endregion

            #region Cholesky decomposition of kff

            var kff = (CSparse.Double.CompressedColumnStorage) Converter.ToCompressedColumnStorage(kffCoord);
            var kfs = (CSparse.Double.CompressedColumnStorage) Converter.ToCompressedColumnStorage(kfsCoord);
            var kss = (CSparse.Double.CompressedColumnStorage) Converter.ToCompressedColumnStorage(kssCoord);

            var chol = new SparseCholesky(kff, ColumnOrdering.MinimumDegreeAtPlusA);

            sp.Stop();

            TraceUtil.WritePerformanceTrace("cholesky decomposition of Kff tooks about {0:#,##0} ms",
                sp.ElapsedMilliseconds);

            
            TraceUtil.WritePerformanceTrace("nnz of kff is {0:#,##0}, ~{1:0.0000}%", kff.Values.Length,
                ((double) kff.Values.Length)/((double) kff.RowCount*kff.ColumnCount));
            sp.Restart();

            #endregion

            var result = new StaticLinearAnalysisResult();
            result.KffCholesky = chol;
            result.Kfs = kfs;
            result.Kss = kss;
            result.Parent = this;
            result.SettlementsLoadCase = config.SettlementsLoadCase;
            
            result.ReleasedMap = rMap;
            result.FixedMap = fMap;
            result.ReversedReleasedMap = rrmap;
            result.ReversedFixedMap = rfmap;

            foreach (var cse in config.LoadCases)
            {
                result.AddAnalysisResult(cse);
            }


            this.lastResult = result;

            foreach (var elm in elements)
            {
                foreach (var nde in elm.Nodes)
                {
                    nde.ConnectedElements.Add(elm);
                }
            }
        }

        #endregion

    }
}
