using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSDLExporter
{
    class IntervalSet<T> where T : IComparable<T>
    {
        private List<Interval<T>> intervals = new List<Interval<T>>(); // TODO: not exactly efficient

        public IntervalSet(){}

        public IntervalSet(List<Interval<T>> intervals)
        {
            this.intervals = intervals;
        }

        public IntervalSet(T lower, T upper)
        {
            intervals.Add(new Interval<T>(lower, upper));
        }

        internal List<Interval<T>> Intervals { get => intervals;}

        public void UnionInterval(Interval<T> newInterval)
        {
            if(intervals.Count == 0)
            {
                intervals.Add(newInterval);
                return;
            }

            // first check if we can insert at the start or the end.
            if(newInterval.upper.CompareTo(intervals[0].lower) < 0)
            {
                intervals.Insert(0, newInterval);
                return;
            }

            if (newInterval.lower.CompareTo(intervals.Last().upper) > 0)
            {
                intervals.Add(newInterval);
                return;
            }

            int insertionIndex = -1;

            // could be optimized by using a binary search. However, since I expect this to consist of at most 3 intervals it's probably not worth it
            // find all intervals that overlap and the insertion index
            for (int i = 0; i < intervals.Count; i++)
            {
                Interval<T> interval = intervals[i];
                if (interval.CompareTo(newInterval) == 0)
                {
                    newInterval = newInterval.ConvexUnion(interval);
                    intervals.RemoveAt(i);
                    insertionIndex = i;
                    i--;
                }               
                else if( insertionIndex != -1) // already passed all overlapping intervals
                {
                    break;
                }
                else if (interval.CompareTo(newInterval) < 0) // no overlaps
                {
                    insertionIndex = i;
                    break;
                }
            }

            intervals.Insert(insertionIndex, newInterval);
        }
    }

    class Interval<T> : IComparable<Interval<T>> where T : IComparable<T>
    {
        public readonly T lower;
        public readonly T upper;

        public Interval(T lower, T upper)
        {
            if (lower.CompareTo(upper) < 0)
            {
                this.lower = lower;
                this.upper = upper;
            }
            else
            {
                this.lower = upper;
                this.upper = lower;
            }
        }

        public int CompareTo(Interval<T> other)
        {
            if (other.upper.CompareTo(lower) < 0)
            {
                return -1;
            }

            if (other.lower.CompareTo(upper) > 0)
            {
                return 1;
            }

            // overlap
            return 0;
        }

        /// <summary>
        /// Returns the convex hull of the union of the two intervals
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public Interval<T> ConvexUnion(Interval<T> other)
        {
            T lower = other.lower.CompareTo(this.lower) < 0 ? other.lower : this.lower;
            T upper = other.upper.CompareTo(this.lower) > 0 ? other.upper : this.upper;

            return new Interval<T>(lower, upper);
        }

        /*public T Range()
        {
            dynamic upper = (dynamic)this.upper;
            dynamic lower = (dynamic)this.lower;

            try
            {
                return upper - lower;
            }
            catch
            {
                throw new Exception("Subtraction is not supported by the type " + typeof(T).FullName);
            }
        }*/
    }
}
