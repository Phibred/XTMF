/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Threading;

namespace Datastructure
{
    public class GatewayLock
    {
        private object Gateway = new object();
        private long ThingsIn = 0;

        /// <summary>
        /// The number of things that are currently being processed
        /// </summary>
        public long Processing { get { return ThingsIn; } }

        /// <summary>
        /// Lock the Gateway until "This" is done
        /// </summary>
        /// <param name="DoThis">What we need to do</param>
        public void Lock(Action DoThis)
        {
            lock ( this.Gateway )
            {
                while ( Interlocked.Read( ref ThingsIn ) > 0 )
                {
                    Thread.Sleep( 0 );
                }
                DoThis();
            }
        }

        /// <summary>
        /// Execute a procedure when the gate
        /// is not being blocked
        /// </summary>
        /// <param name="DoThis">The thing to do</param>
        public void PassThrough(Action DoThis)
        {
            lock ( this.Gateway )
            {
                Interlocked.Increment( ref ThingsIn );
            }
            DoThis();
            Interlocked.Decrement( ref ThingsIn );
        }
    }
}