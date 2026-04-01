# Unique counter

```C#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HyperLogLog
{
    /// <summary>
    /// HyperLogLog algorithm for approximate distinct count
    /// Uses less memory than exact counting with O(1) time complexity
    /// </summary>
    public class HyperLogLog
    {
        private readonly int _precision;          // Number of bits for register index (p)
        private readonly int _registerCount;      // Number of registers = 2^p
        private readonly byte[] _registers;       // Array to store maximum leading zero counts
        private readonly double _alpha;           // Alpha constant for bias correction
        private readonly HashAlgorithm _hasher;   // Hash function
        private readonly int _hashBitSize;        // Number of bits in hash output
        
        /// <summary>
        /// Creates HyperLogLog with given precision
        /// </summary>
        /// <param name="precision">Number of bits for register index (4-16). Higher precision = better accuracy but more memory</param>
        public HyperLogLog(int precision = 14)
        {
            if (precision < 4 || precision > 16)
                throw new ArgumentException("Precision must be between 4 and 16", nameof(precision));
            
            _precision = precision;
            _registerCount = 1 << precision; // 2^precision
            _registers = new byte[_registerCount];
            _hasher = SHA256.Create();
            _hashBitSize = 256; // SHA256 produces 256 bits
            
            // Calculate alpha constant based on precision
            _alpha = CalculateAlpha();
        }
        
        /// <summary>
        /// Adds an element to the HyperLogLog
        /// </summary>
        public void Add<T>(T element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            
            // Get hash of the element
            byte[] hashBytes = GetHashBytes(element);
            
            // Extract register index from first p bits
            int registerIndex = GetRegisterIndex(hashBytes);
            
            // Count leading zeros in remaining bits
            byte leadingZeros = CountLeadingZeros(hashBytes, _precision);
            
            // Update register with maximum leading zeros
            if (leadingZeros > _registers[registerIndex])
            {
                _registers[registerIndex] = leadingZeros;
            }
        }
        
        /// <summary>
        /// Adds a range of elements to the HyperLogLog
        /// </summary>
        public void AddRange<T>(IEnumerable<T> elements)
        {
            foreach (var element in elements)
            {
                Add(element);
            }
        }
        
        /// <summary>
        /// Estimates the number of distinct elements
        /// </summary>
        public long Estimate()
        {
            // Calculate harmonic mean of the registers
            double sum = 0;
            int zeroRegisters = 0;
            
            for (int i = 0; i < _registerCount; i++)
            {
                sum += Math.Pow(2, -_registers[i]);
                if (_registers[i] == 0)
                    zeroRegisters++;
            }
            
            // Initial estimate
            double estimate = _alpha * _registerCount * _registerCount / sum;
            
            // Apply bias correction for small cardinalities
            if (estimate <= 2.5 * _registerCount)
            {
                // Linear counting for small cardinalities
                if (zeroRegisters > 0)
                {
                    estimate = _registerCount * Math.Log(_registerCount / (double)zeroRegisters);
                }
            }
            else if (estimate > Math.Pow(2, 32) / 30.0)
            {
                // Large cardinality correction
                estimate = -Math.Pow(2, 32) * Math.Log(1 - estimate / Math.Pow(2, 32));
            }
            
            return (long)Math.Round(estimate);
        }
        
        /// <summary>
        /// Merges another HyperLogLog into this one (union operation)
        /// </summary>
        public void Merge(HyperLogLog other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            
            if (other._precision != _precision)
                throw new ArgumentException("Cannot merge HyperLogLogs with different precision");
            
            for (int i = 0; i < _registerCount; i++)
            {
                if (other._registers[i] > _registers[i])
                {
                    _registers[i] = other._registers[i];
                }
            }
        }
        
        /// <summary>
        /// Gets the relative error of the estimate
        /// </summary>
        public double GetRelativeError()
        {
            // Standard error for HyperLogLog is approximately 1.04 / sqrt(2^precision)
            return 1.04 / Math.Sqrt(_registerCount);
        }
        
        /// <summary>
        /// Gets memory usage in bytes
        /// </summary>
        public long GetMemoryUsage()
        {
            return _registers.Length; // Each register is 1 byte
        }
        
        /// <summary>
        /// Resets the HyperLogLog (clears all registers)
        /// </summary>
        public void Reset()
        {
            Array.Clear(_registers, 0, _registers.Length);
        }
        
        /// <summary>
        /// Gets the current maximum register value
        /// </summary>
        public int GetMaxRegisterValue()
        {
            return _registers.Max();
        }
        
        /// <summary>
        /// Gets register distribution for analysis
        /// </summary>
        public Dictionary<int, int> GetRegisterDistribution()
        {
            var distribution = new Dictionary<int, int>();
            foreach (var value in _registers)
            {
                distribution[value] = distribution.GetValueOrDefault(value) + 1;
            }
            return distribution;
        }
        
        private byte[] GetHashBytes<T>(T element)
        {
            string elementString = element.ToString();
            byte[] inputBytes = Encoding.UTF8.GetBytes(elementString);
            return _hasher.ComputeHash(inputBytes);
        }
        
        private int GetRegisterIndex(byte[] hashBytes)
        {
            // Extract first p bits from hash
            int index = 0;
            int bitsNeeded = _precision;
            int currentByte = 0;
            int bitPosition = 0;
            
            while (bitsNeeded > 0)
            {
                if (bitPosition >= 8)
                {
                    currentByte++;
                    bitPosition = 0;
                }
                
                int bitsToTake = Math.Min(bitsNeeded, 8 - bitPosition);
                int mask = (1 << bitsToTake) - 1;
                int value = (hashBytes[currentByte] >> bitPosition) & mask;
                
                index |= value << (_precision - bitsNeeded);
                bitsNeeded -= bitsToTake;
                bitPosition += bitsToTake;
            }
            
            return index;
        }
        
        private byte CountLeadingZeros(byte[] hashBytes, int startBit)
        {
            int zeroCount = 0;
            int currentByte = startBit / 8;
            int bitPosition = startBit % 8;
            int remainingBits = _hashBitSize - startBit;
            
            while (remainingBits > 0 && zeroCount < 64) // Limit to 64 bits
            {
                int bitsToCheck = Math.Min(remainingBits, 8 - bitPosition);
                int mask = (1 << bitsToCheck) - 1;
                int value = (hashBytes[currentByte] >> bitPosition) & mask;
                
                if (value != 0)
                {
                    // Count leading zeros in this byte
                    zeroCount += CountLeadingZerosInInt(value, bitsToCheck);
                    break;
                }
                else
                {
                    zeroCount += bitsToCheck;
                }
                
                remainingBits -= bitsToCheck;
                currentByte++;
                bitPosition = 0;
            }
            
            // Add 1 because we count the position of the first 1
            return (byte)(zeroCount + 1);
        }
        
        private int CountLeadingZerosInInt(int value, int bits)
        {
            int count = 0;
            int mask = 1 << (bits - 1);
            
            while (bits > 0 && (value & mask) == 0)
            {
                count++;
                mask >>= 1;
                bits--;
            }
            
            return count;
        }
        
        private double CalculateAlpha()
        {
            switch (_precision)
            {
                case 4: return 0.673;
                case 5: return 0.697;
                case 6: return 0.709;
                default: return 0.7213 / (1 + 1.079 / _registerCount);
            }
        }
        
        /// <summary>
        /// Gets statistics about the HyperLogLog
        /// </summary>
        public HyperLogLogStats GetStatistics()
        {
            var distribution = GetRegisterDistribution();
            return new HyperLogLogStats
            {
                Precision = _precision,
                RegisterCount = _registerCount,
                MemoryBytes = GetMemoryUsage(),
                EstimatedCardinality = Estimate(),
                RelativeError = GetRelativeError(),
                MaxRegisterValue = GetMaxRegisterValue(),
                ZeroRegisters = distribution.GetValueOrDefault(0, 0),
                RegisterDistribution = distribution
            };
        }
    }
    
    /// <summary>
    /// HyperLogLog with generic support for different hash functions
    /// </summary>
    public class HyperLogLog<T> : HyperLogLog
    {
        public HyperLogLog(int precision = 14) : base(precision)
        {
        }
        
        public void Add(T element)
        {
            base.Add(element);
        }
        
        public void AddRange(IEnumerable<T> elements)
        {
            base.AddRange(elements);
        }
    }
    
    /// <summary>
    /// HyperLogLog statistics class
    /// </summary>
    public class HyperLogLogStats
    {
        public int Precision { get; set; }
        public int RegisterCount { get; set; }
        public long MemoryBytes { get; set; }
        public long EstimatedCardinality { get; set; }
        public double RelativeError { get; set; }
        public int MaxRegisterValue { get; set; }
        public int ZeroRegisters { get; set; }
        public Dictionary<int, int> RegisterDistribution { get; set; }
        
        public override string ToString()
        {
            return $"HyperLogLog Stats:\n" +
                   $"  Precision: {Precision} (2^{Precision} = {RegisterCount} registers)\n" +
                   $"  Memory: {MemoryBytes} bytes\n" +
                   $"  Estimated Cardinality: {EstimatedCardinality:N0}\n" +
                   $"  Relative Error: ±{RelativeError:P2}\n" +
                   $"  Max Register Value: {MaxRegisterValue}\n" +
                   $"  Zero Registers: {ZeroRegisters} ({ZeroRegisters/(double)RegisterCount:P2})";
        }
    }
    
    /// <summary>
    /// HyperLogLog with cardinality estimation for different confidence intervals
    /// </summary>
    public class HyperLogLogWithConfidence : HyperLogLog
    {
        public HyperLogLogWithConfidence(int precision = 14) : base(precision)
        {
        }
        
        public (long estimate, double lowerBound, double upperBound) EstimateWithConfidence(double confidenceLevel = 0.95)
        {
            long estimate = Estimate();
            double standardError = GetRelativeError();
            
            // Calculate z-score for confidence level
            double zScore = GetZScore(confidenceLevel);
            double marginOfError = standardError * estimate * zScore;
            
            return (
                estimate: estimate,
                lowerBound: Math.Max(0, estimate - marginOfError),
                upperBound: estimate + marginOfError
            );
        }
        
        private double GetZScore(double confidenceLevel)
        {
            // Common z-scores for confidence levels
            return confidenceLevel switch
            {
                0.80 => 1.28,
                0.85 => 1.44,
                0.90 => 1.645,
                0.95 => 1.96,
                0.99 => 2.576,
                _ => 1.96 // Default to 95%
            };
        }
    }
    
    /// <summary>
    /// Program with usage examples and benchmarks
    /// </summary>
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("=== HyperLogLog Demonstration ===\n");
            
            // Example 1: Basic usage
            Console.WriteLine("Example 1: Basic Usage");
            var hll = new HyperLogLog(14);
            
            // Add some elements
            for (int i = 0; i < 10000; i++)
            {
                hll.Add($"item_{i}");
            }
            
            var stats = hll.GetStatistics();
            Console.WriteLine(stats);
            Console.WriteLine();
            
            // Example 2: Different precision levels
            Console.WriteLine("Example 2: Precision Comparison");
            ComparePrecisionLevels();
            Console.WriteLine();
            
            // Example 3: Generic typed HyperLogLog
            Console.WriteLine("Example 3: Generic Typed HyperLogLog");
            var typedHll = new HyperLogLog<string>(12);
            
            var words = new[] { "apple", "banana", "cherry", "date", "apple", "banana", "elderberry" };
            typedHll.AddRange(words);
            
            Console.WriteLine($"Distinct words: {typedHll.Estimate()}");
            Console.WriteLine($"Actual distinct: {words.Distinct().Count()}");
            Console.WriteLine();
            
            // Example 4: Merge operation
            Console.WriteLine("Example 4: Merging HyperLogLogs");
            var hll1 = new HyperLogLog(12);
            var hll2 = new HyperLogLog(12);
            
            // Add elements to first HLL
            for (int i = 0; i < 5000; i++)
            {
                hll1.Add($"item_{i}");
            }
            
            // Add elements to second HLL
            for (int i = 2500; i < 7500; i++)
            {
                hll2.Add($"item_{i}");
            }
            
            Console.WriteLine($"HLL1 estimate: {hll1.Estimate():N0}");
            Console.WriteLine($"HLL2 estimate: {hll2.Estimate():N0}");
            
            // Merge
            hll1.Merge(hll2);
            Console.WriteLine($"Merged estimate: {hll1.Estimate():N0}");
            Console.WriteLine($"Expected union cardinality: 7500");
            Console.WriteLine();
            
            // Example 5: Confidence intervals
            Console.WriteLine("Example 5: Confidence Intervals");
            var hllConfidence = new HyperLogLogWithConfidence(14);
            
            for (int i = 0; i < 100000; i++)
            {
                hllConfidence.Add($"element_{i}");
            }
            
            var (estimate, lower, upper) = hllConfidence.EstimateWithConfidence(0.95);
            Console.WriteLine($"Estimate: {estimate:N0}");
            Console.WriteLine($"95% Confidence Interval: [{lower:N0}, {upper:N0}]");
            Console.WriteLine($"Actual: 100,000");
            Console.WriteLine();
            
            // Example 6: Performance benchmark
            Console.WriteLine("Example 6: Performance Benchmark");
            Benchmark();
            Console.WriteLine();
            
            // Example 7: Memory efficiency demonstration
            Console.WriteLine("Example 7: Memory Efficiency");
            DemonstrateMemoryEfficiency();
        }
        
        private static void ComparePrecisionLevels()
        {
            int[] precisions = { 10, 12, 14, 16 };
            int cardinality = 100000;
            
            Console.WriteLine($"Comparing precision levels for {cardinality:N0} distinct elements:\n");
            Console.WriteLine("Precision | Registers | Memory  | Estimate    | Error     | Accuracy");
            Console.WriteLine("----------|-----------|---------|-------------|-----------|---------");
            
            foreach (int precision in precisions)
            {
                var hll = new HyperLogLog(precision);
                
                for (int i = 0; i < cardinality; i++)
                {
                    hll.Add($"element_{i}");
                }
                
                long estimate = hll.Estimate();
                double error = Math.Abs(estimate - cardinality) / (double)cardinality * 100;
                int registers = 1 << precision;
                long memory = registers;
                
                Console.WriteLine($"{precision,9} | {registers,9} | {memory,6} B | {estimate,11:N0} | {error,7:F2}% | {GetAccuracyGrade(error)}");
            }
        }
        
        private static string GetAccuracyGrade(double errorPercent)
        {
            return errorPercent switch
            {
                < 1 => "Excellent",
                < 2 => "Very Good",
                < 3 => "Good",
                < 5 => "Fair",
                _ => "Poor"
            };
        }
        
        private static void Benchmark()
        {
            int iterations = 1000000;
            var hll = new HyperLogLog(14);
            
            // Measure insertion performance
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                hll.Add($"element_{i}");
            }
            
            stopwatch.Stop();
            double timePerOperation = stopwatch.Elapsed.TotalMilliseconds / iterations;
            
            Console.WriteLine($"Inserted {iterations:N0} elements");
            Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Time per operation: {timePerOperation:F4} ms");
            Console.WriteLine($"Memory usage: {hll.GetMemoryUsage()} bytes");
            
            // Measure estimate performance
            stopwatch.Restart();
            long estimate = hll.Estimate();
            stopwatch.Stop();
            
            Console.WriteLine($"Estimate: {estimate:N0}");
            Console.WriteLine($"Estimate time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        }
        
        private static void DemonstrateMemoryEfficiency()
        {
            int cardinality = 10000000; // 10 million
            Console.WriteLine($"Counting distinct elements among {cardinality:N0} items...");
            
            // HyperLogLog approach
            var hll = new HyperLogLog(14);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < cardinality; i++)
            {
                hll.Add($"item_{i % 500000}"); // 500,000 distinct items
            }
            
            stopwatch.Stop();
            
            long hllEstimate = hll.Estimate();
            long hllMemory = hll.GetMemoryUsage();
            long hllTime = stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine("\nHyperLogLog Results:");
            Console.WriteLine($"  Estimated distinct: {hllEstimate:N0}");
            Console.WriteLine($"  Memory: {hllMemory:N0} bytes ({hllMemory / 1024.0:F2} KB)");
            Console.WriteLine($"  Time: {hllTime} ms");
            
            // Compare with HashSet approach (would be huge memory)
            Console.WriteLine("\nHashSet (Exact Counting) would require:");
            long exactMemory = cardinality * 24; // Approximate memory per item
            Console.WriteLine($"  Memory: ~{exactMemory:N0} bytes ({exactMemory / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"  Memory Savings: {(exactMemory / (double)hllMemory):F0}x");
        }
    }
    
    /// <summary>
    /// Extension methods for easy integration
    /// </summary>
    public static class HyperLogLogExtensions
    {
        public static long ApproximateCount<T>(this IEnumerable<T> source, int precision = 14)
        {
            var hll = new HyperLogLog<T>(precision);
            hll.AddRange(source);
            return hll.Estimate();
        }
        
        public static HyperLogLog<T> ToHyperLogLog<T>(this IEnumerable<T> source, int precision = 14)
        {
            var hll = new HyperLogLog<T>(precision);
            hll.AddRange(source);
            return hll;
        }
    }
}
```