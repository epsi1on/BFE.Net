﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization;
using System.Text;

namespace BriefFiniteElementNet
{
    /// <summary>
    /// Represents a uniform load with specified magnitude which is applying to an <see cref="Element1D"/> body.
    /// </summary>
    [Serializable]
    public class UniformLoad1D : Load1D
    {
        #region Members

        private double magnitude;

        /// <summary>
        /// Gets or sets the magnitude.
        /// </summary>
        /// <remarks>
        /// Value is magnitude of distributed load, the unit is [Force/Length]
        /// </remarks>
        /// <value>
        /// The magnitude of distributed load along the member.
        /// </value>
        public double Magnitude
        {
            get { return magnitude; }
            set { magnitude = value; }
        }

        #endregion

        #region Methods

        public override Force[] GetEquivalentNodalLoads(Element element)
        {


            if (element is FrameElement2Node)
            {
                var frElm = element as FrameElement2Node;

                var l = (frElm.EndNode.Location - frElm.StartNode.Location).Length;

                var w = GetLocalDistributedLoad(element as Element1D);

                var localEndForces = new Force[2];

                if (frElm.HingedAtEnd & frElm.HingedAtStart)
                {
                    localEndForces[0] = new Force(w.X*l/2, w.Y*l/2, w.Z*l/2, 0, 0, 0);
                    localEndForces[1] = new Force(w.X*l/2, w.Y*l/2, w.Z*l/2, 0, 0, 0);
                }
                else if (!frElm.HingedAtEnd & frElm.HingedAtStart)
                {

                }
                else if (frElm.HingedAtEnd & !frElm.HingedAtStart)
                {

                }
                else if (!frElm.HingedAtEnd & !frElm.HingedAtStart)
                {
                    localEndForces[0] = new Force(w.X*l/2, w.Y*l/2, w.Z*l/2, 0, -w.Z*l*l/12.0, w.Y*l*l/12.0);
                    localEndForces[1] = new Force(w.X*l/2, w.Y*l/2, w.Z*l/2, 0, w.Z*l*l/12.0, -w.Y*l*l/12.0);
                }


                for (var i = 0; i < element.Nodes.Length; i++)
                {
                    var frc = localEndForces[i];
                    localEndForces[i] = new Force(frElm.TransformLocalToGlobal(frc.Forces),
                        frElm.TransformLocalToGlobal(frc.Moments));
                }

                return localEndForces;
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the local distributed load.
        /// </summary>
        /// <param name="elm">The elm.</param>
        /// <returns></returns>
        /// <remarks>
        /// Gets a vector that its components shows Wx, Wy and Wz in local coordination system of <see cref="elm"/>
        /// </remarks>
        private Vector GetLocalDistributedLoad(Element1D elm)
        {
            if (coordinationSystem == CoordinationSystem.Local)
            {
                return new Vector(
                    direction == LoadDirection.X ? this.magnitude : 0,
                    direction == LoadDirection.Y ? this.magnitude : 0,
                    direction == LoadDirection.Z ? this.magnitude : 0);
            }

            if (elm is FrameElement2Node)
            {
                var frElm = elm as FrameElement2Node;

                var w = new Vector();


                var globalVc = new Vector(
                    direction == LoadDirection.X ? this.magnitude : 0,
                    direction == LoadDirection.Y ? this.magnitude : 0,
                    direction == LoadDirection.Z ? this.magnitude : 0);

                w = frElm.TransformGlobalToLocal(globalVc);

                return w;
            }

            throw new NotImplementedException();
        }

        public override Force GetInternalForceAt(Element1D elm, double x)
        {
            if (elm is FrameElement2Node)
            {
                var frElm = elm as FrameElement2Node;

                var l = (frElm.EndNode.Location - frElm.StartNode.Location).Length;
                var w = GetLocalDistributedLoad(elm);

                var f1 = -GetEquivalentNodalLoads(elm)[0];
                var f2 = new Force(new Vector(w.X*x, w.Y*x, w.Z*x), new Vector());

                var buf = f1.Move(new Point(0, 0, 0), new Point(x, 0, 0)) +
                          f2.Move(new Point(x/2, 0, 0), new Point(x, 0, 0));

                return -buf;
            }

            throw new NotImplementedException();
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="UniformLoad1D"/> class.
        /// </summary>
        /// <param name="magnitude">The magnitude.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="sys">The system.</param>
        /// <param name="cse">The cse.</param>
        public UniformLoad1D(double magnitude, LoadDirection direction, CoordinationSystem sys, LoadCase cse)
        {
            this.magnitude = magnitude;
            this.coordinationSystem = sys;
            this.direction = direction;
            this.Case = cse;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UniformLoad1D"/> class.
        /// </summary>
        /// <param name="magnitude">The magnitude.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="sys">The system.</param>
        public UniformLoad1D(double magnitude, LoadDirection direction, CoordinationSystem sys)
        {
            this.magnitude = magnitude;
            this.coordinationSystem = sys;
            this.direction = direction;
        }


        public UniformLoad1D()
        {
        }

        #endregion


        #region Serialization stuff and constructor

        /// <summary>
        /// Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> to populate with data.</param>
        /// <param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext" />) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("magnitude", magnitude);
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="UniformLoad1D"/> class.
        /// </summary>
        /// <param name="info">The information.</param>
        /// <param name="context">The context.</param>
        protected UniformLoad1D(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.magnitude = info.GetDouble("magnitude");
        }

        #endregion


    }
}
