﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BriefFiniteElementNet.Common;
using BriefFiniteElementNet.Elements;
using CSparse.Storage;

namespace BriefFiniteElementNet.ElementHelpers
{
    public class TetrahedronHelper : IElementHelper
    {
        public Element TargetElement { get; set; }

        /// <inheritdoc/>
        public Matrix GetBMatrixAt(Element targetElement, params double[] isoCoords)
        {
            //http://what-when-how.com/the-finite-element-method/fem-for-3d-solids-finite-element-method-part-1/

            var tetra = targetElement as TetrahedronElement;

            var liv = targetElement.AllocateFromPool(4, 4);

            {
                var ps = tetra.Nodes.Select(i => i.Location).ToArray();

                {//eq. 9.11
                    liv.SetColumn(0, new double[] { 1, 1, 1, 1 });
                    liv.SetColumn(1, new double[] { ps[0].X, ps[1].X, ps[2].X, ps[3].X });
                    liv.SetColumn(2, new double[] { ps[0].Y, ps[1].Y, ps[2].Y, ps[3].Y });
                    liv.SetColumn(3, new double[] { ps[0].Z, ps[1].Z, ps[2].Z, ps[3].Z });
                }
            }
            
            var ls = liv.Inverse();// tetra.IsoCoordsToLocalCoords(isoCoords);

            var v = ls.Determinant() * 1 / 6.0;

            ls.Scale(6 * v);

            var buf = targetElement.AllocateFromPool(6, 12);

            {//eq. 9.18
                double a1 = ls[0, 0]; double b1 = ls[0, 1]; double c1 = ls[0, 2]; double d1 = ls[0, 3];
                double a2 = ls[1, 0]; double b2 = ls[1, 1]; double c2 = ls[1, 2]; double d2 = ls[1, 3];
                double a3 = ls[2, 0]; double b3 = ls[2, 1]; double c3 = ls[2, 2]; double d3 = ls[2, 3];
                double a4 = ls[3, 0]; double b4 = ls[3, 1]; double c4 = ls[3, 2]; double d4 = ls[3, 3];

                buf.SetRow(0, new[] { b1, 0, 0, b2, 0, 0, b3, 0, 0, b4, 0, 0 });
                buf.SetRow(1, new[] { 0, c1, 0, 0, c2, 0, 0, c3, 0, 0, c4, 0, });
                buf.SetRow(2, new[] { 0, 0, d1, 0, 0, d2, 0, 0, d3, 0, 0, d4, });
                buf.SetRow(3, new[] { c1, b1, 0, c2, b2, 0, c3, b3, 0, c4, b4, 0 });
                buf.SetRow(4, new[] { 0, d1, c1, 0, d2, c2, 0, d3, c3, 0, d4, c4 });
                buf.SetRow(5, new[] { d1, 0, b1, d2, 0, b2, d3, 0, b3, d4, 0, b4 });
            }

            return buf.AsMatrix();
        }

        

        /// <inheritdoc/>
        public Matrix GetDMatrixAt(Element targetElement, params double[] isoCoords)
        {
            var tetra = targetElement as TetrahedronElement;

            var buf = targetElement.AllocateFromPool(6, 6);

            //Gets the consitutive matrix. Only for isotropic materials!!!If orthotropic is needed: check http://web.mit.edu/16.20/homepage/3_Constitutive/Constitutive_files/module_3_with_solutions.pdf
            var props = tetra.Material.GetMaterialPropertiesAt(isoCoords);

            //material considered to be isotropic
            var miu = props.NuXy;
            var e = props.Ex;

            // var D = targetElement.MatrixPool.Allocate(6, 6);

            var s = (1 - miu);
            // TODO: MAT - use matrix pool
            var D = Matrix.OfRowMajor(6, 6, new double[] { 1, miu / s, miu / s, 0, 0, 0, miu / s, 1, miu / s, 0, 0, 0, miu / s, miu / s, 1, 0, 0, 0, 0, 0, 0,
                (1 - 2 * miu) / (2 * s), 0, 0, 0, 0, 0, 0, (1 - 2 * miu) / (2 * s), 0, 0, 0, 0, 0, 0, (1 - 2 * miu) / (2 * s) });

            D.Scale(e * (1 - miu) / ((1 + miu) * (1 - 2 * miu)));

            return D.AsMatrix();
        }

        public Matrix GetRhoMatrixAt(Element targetElement, params double[] isoCoords)
        {
            throw new NotImplementedException();
        }

        public Matrix GetMuMatrixAt(Element targetElement, params double[] isoCoords)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Matrix GetNMatrixAt(Element targetElement, params double[] isoCoords)
        {
            //http://what-when-how.com/the-finite-element-method/fem-for-3d-solids-finite-element-method-part-1/

            var tetra = targetElement as Tetrahedral;

            var locs = targetElement.AllocateFromPool(4, 4);

            var ps = tetra.Nodes.Select(i => i.Location).ToArray();

            {//eq. 9.11
                locs.SetColumn(0, new double[] { 1, 1, 1, 1 });
                locs.SetColumn(1, new double[] { ps[0].X, ps[1].X, ps[2].X, ps[3].X });
                locs.SetColumn(2, new double[] { ps[0].Y, ps[1].Y, ps[2].Y, ps[3].Y });
                locs.SetColumn(3, new double[] { ps[0].Z, ps[1].Z, ps[2].Z, ps[3].Z });
            }


            var local = tetra.IsoCoordsToLocalCoords(isoCoords);

            var buf = locs.Solve(new double[] { 1, local[0], local[1], local[2] });//9.15

            return new Matrix(4, 1, buf);
        }

        /// <inheritdoc/>
        public Matrix GetJMatrixAt(Element targetElement, params double[] isoCoords)
        {
            var tet = targetElement as TetrahedronElement;

            if (tet == null)
                throw new Exception();

            var n1 = tet.Nodes[0].Location;
            var n2 = tet.Nodes[1].Location;
            var n3 = tet.Nodes[2].Location;
            var n4 = tet.Nodes[3].Location;

            var buf = targetElement.AllocateFromPool(3, 3);

            // TODO: MAT - set values directly
            buf.SetRow(0, new double[] { n2.X - n1.X, n3.X - n1.X, n4.X - n1.X });
            buf.SetRow(1, new double[] { n2.Y - n1.Y, n3.Y - n1.Y, n4.Y - n1.Y });
            buf.SetRow(2, new double[] { n2.Z - n1.Z, n3.Z - n1.Z, n4.Z - n1.Z });

            return buf.AsMatrix();
        }

        /// <inheritdoc/>
        public Matrix CalcLocalStiffnessMatrix(Element targetElement)
        {
            return ElementHelperExtensions.CalcLocalKMatrix_Tetrahedron(this, targetElement);
        }

        public Matrix CalcLocalMassMatrix(Element targetElement)
        {
            throw new NotImplementedException();
        }

        public Matrix CalcLocalDampMatrix(Element targetElement)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public ElementPermuteHelper.ElementLocalDof[] GetDofOrder(Element targetElement)
        {
            var buf = new ElementPermuteHelper.ElementLocalDof[]
            {
                new ElementPermuteHelper.ElementLocalDof(0, DoF.Dx),
                new ElementPermuteHelper.ElementLocalDof(0, DoF.Dy),
                new ElementPermuteHelper.ElementLocalDof(0, DoF.Dz),

                new ElementPermuteHelper.ElementLocalDof(1, DoF.Dx),
                new ElementPermuteHelper.ElementLocalDof(1, DoF.Dy),
                new ElementPermuteHelper.ElementLocalDof(1, DoF.Dz),

                new ElementPermuteHelper.ElementLocalDof(2, DoF.Dx),
                new ElementPermuteHelper.ElementLocalDof(2, DoF.Dy),
                new ElementPermuteHelper.ElementLocalDof(2, DoF.Dz),

                new ElementPermuteHelper.ElementLocalDof(3, DoF.Dx),
                new ElementPermuteHelper.ElementLocalDof(3, DoF.Dy),
                new ElementPermuteHelper.ElementLocalDof(3, DoF.Dz),

            };

            return buf;
        }

        /// <inheritdoc/>
        public IEnumerable<Tuple<DoF, double>> GetLocalInternalForceAt(Element targetElement,
            Displacement[] globalDisplacements, params double[] isoCoords)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public bool DoesOverrideKMatrixCalculation(Element targetElement, Matrix transformMatrix)
        {
            return false;
        }

        /// <inheritdoc/>
        public int[] GetNMaxOrder(Element targetElement)
        {
            throw new NotImplementedException();
        }

        public int[] GetBMaxOrder(Element targetElement)
        {
            return new[] {0, 0, 0};
        }

        public int[] GetDetJOrder(Element targetElement)
        {
            return new[] { 0, 0, 0 };
        }

        public IEnumerable<Tuple<DoF, double>> GetLoadInternalForceAt(Element targetElement, ElementalLoad load,
            double[] isoLocation)
        {
            throw new NotImplementedException();
        }

        public Displacement GetLoadDisplacementAt(Element targetElement, ElementalLoad load, double[] isoLocation)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Displacement GetLocalDisplacementAt(Element targetElement, Displacement[] localDisplacements, params double[] isoCoords)
        {
            throw new NotImplementedException();
        }

        public Force[] GetLocalEquivalentNodalLoads(Element targetElement, ElementalLoad load)
        {
            throw new NotImplementedException();
        }

        public void AddStiffnessComponents(CoordinateStorage<double> global)
        {
            throw new NotImplementedException();
        }

        public GeneralStressTensor GetLocalStressAt(Element targetElement, Displacement[] localDisplacements, params double[] isoCoords)
        {
            throw new NotImplementedException();
        }

        public GeneralStressTensor GetLoadStressAt(Element targetElement, ElementalLoad load, double[] isoLocation)
        {
            throw new NotImplementedException();
        }

        public GeneralStressTensor GetLocalInternalStressAt(Element targetElement, Displacement[] localDisplacements, params double[] isoCoords)
        {
            throw new NotImplementedException();
        }
    }
}
