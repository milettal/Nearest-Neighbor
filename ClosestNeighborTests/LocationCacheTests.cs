using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ClosestNeighborWpf;
using GeoCoordinatePortable;
using KdTree;
using KdTree.Math;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClosestNeighborTests {

	[TestClass]
	public class LocationCacheTests {

		private Random _random = new Random();

		///<summary>Tests that searching by address is sped up by the caching strategies implemented within the cache.
		///The first scenario is that we have already searched for a term before. The difference should be magnitudes faster.
		///The second scenario is that we have searched for a partial of the string before. The difference depends on how much it narrows the results.</summary>
		[TestMethod]
		public void AddressSearch_Speed() {
			List<Location> listLocations = Utils.ReadCSVFile();
			#region Exact Search
			LocationCache cache = new LocationCache(listLocations);
			Stopwatch firstHit = new Stopwatch();
			firstHit.Start();
			_ = cache.GetAddressMatches("ab");
			firstHit.Stop();
			Stopwatch secondHit = new Stopwatch();
			secondHit.Start();
			_ = cache.GetAddressMatches("ab");
			secondHit.Stop();
			Assert.IsTrue(firstHit.ElapsedTicks > secondHit.ElapsedTicks);
			Console.WriteLine($"First Hit: {firstHit.ElapsedTicks} ticks\nSecond Hit: {secondHit.ElapsedTicks} ticks\n");
			#endregion
			#region Partial Search
			LocationCache cachePartial = new LocationCache(listLocations);
			cachePartial.GetAddressMatches("a");
			Stopwatch partial = new Stopwatch();
			partial.Start();
			_ = cachePartial.GetAddressMatches("ab");
			partial.Stop();
			LocationCache cacheNoPartial = new LocationCache(listLocations);
			Stopwatch noPartial = new Stopwatch();
			noPartial.Start();
			_ = cacheNoPartial.GetAddressMatches("ab");
			noPartial.Stop();
			Assert.IsTrue(noPartial.ElapsedTicks > partial.ElapsedTicks);
			Console.WriteLine($"Partial: {partial.ElapsedTicks} ticks\nNo Partial: {noPartial.ElapsedTicks} ticks\n");
			#endregion
		}

		///<summary>Tests that searching by address works as expected.</summary>
		[TestMethod]
		public void AddressSearch_Accuracy() {
			List<Location> listInitialLocations = new List<Location> {
				new Location(1, "Test1", "", "", "", 0, 0),
				new Location(2, "Te3", "", "", "", 0, 0),
				new Location(3, "abc4ewq", "", "", "", 0, 0),
			};
			LocationCache cache = new LocationCache(listInitialLocations);
			List<Location> results = cache.GetAddressMatches("TE");
			Assert.AreEqual(2, results.Count);
			Assert.IsTrue(results.Any(x => x.Id == 1));
			Assert.IsTrue(results.Any(x => x.Id == 2));
			results = cache.GetAddressMatches("e3");
			Assert.AreEqual(1, results.Count);
			Assert.IsTrue(results.Any(x => x.Id == 2));
			results = cache.GetAddressMatches("ET");
			Assert.AreEqual(0, results.Count);
			results = cache.GetAddressMatches("");
			Assert.AreEqual(0, results.Count);
		}

		///<summary>This is a speed test between my own implementation I did with the implementation using the Kd tree.
		///This shows how much of an improvement the kd tree is and also shows my original approach to the issue.
		///I will be testing preloading the data as well as querying for nearest neighbors for 100 random locations.</summary>
		[TestMethod]
		public async Task NearestNeighbor_Speed() {
			List<Location> listLocations = Utils.ReadCSVFile();
			List<Location> listRandomLocations = GetRandomLocations(listLocations, 100);
			#region Kd Tree
			Stopwatch watchKdTree = new Stopwatch();
			watchKdTree.Start();
			KdTree<double, Location> kdTree = FillKdTree(listLocations);
			foreach(Location randomLoc in listRandomLocations) {
				_ = kdTree.GetNearestNeighbours(new double[3] { randomLoc.X, randomLoc.Y, randomLoc.Z }, 10);
			}
			watchKdTree.Stop();
			#endregion
			#region Original Approach
			Stopwatch watchOriginalApproach = new Stopwatch();
			watchOriginalApproach.Start();
			ConcurrentDictionary<Location, List<Location>> dictNearestNeighbors = await FillOriginalApproach(listLocations, 10);
			foreach(Location randomLoc in listRandomLocations) {
				_ = dictNearestNeighbors[randomLoc];
			}
			watchOriginalApproach.Stop();
			#endregion
			Console.WriteLine($"Kd Tree: {watchKdTree.ElapsedMilliseconds}ms");
			Console.WriteLine($"Original: {watchOriginalApproach.ElapsedMilliseconds}ms");
			Assert.IsTrue(watchKdTree.ElapsedMilliseconds < watchOriginalApproach.ElapsedMilliseconds);
		}

		///<summary>The purpose of this test is to show that both my original method and the kd tree method produce nearly the same results. The Kd tree method
		///converts the latitude and longitude to cartesian coordinates which loses some accuracy. This shouldn't be too much of a problem as this sample set is in the same
		///region.
		///My original approach uses the haversine formula implemented by a third party library.</summary>
		[TestMethod]
		public async Task NearestNeighbor_Accuracy() {
			List<Location> listLocations = Utils.ReadCSVFile();
			KdTree<double, Location> kdTree = FillKdTree(listLocations);
			int orderDifferent = 0;
			int resultsDifferent = 0;
			ConcurrentDictionary<Location, List<Location>> dictNearestNeighbors = await FillOriginalApproach(listLocations, 10);
			foreach(Location loc in listLocations) {
				var v = kdTree.GetNearestNeighbours(new double[3] { loc.X, loc.Y, loc.Z }, 11);
				List<Location> kdResult = kdTree.GetNearestNeighbours(new double[3] { loc.X, loc.Y, loc.Z }, 11)
					.Where(x => x.Value != loc)
					.Select(x => x.Value)
					.ToList();
				List<Location> originalResult = dictNearestNeighbors[loc];
				if(kdResult.Count != 10 || originalResult.Count != 10) {
					Assert.Fail("Incorrect number of nearest neighbors");
				}
				bool diffOrder = false;
				bool diffResults = false;
				for(int i = 0; i < kdResult.Count; i++) {
					if(kdResult[i].Id != originalResult[i].Id) {
						diffOrder = true;
					}
					if(!originalResult.Contains(kdResult[i])) {
						diffResults = true;
					}
				}
				if(diffResults) {
					resultsDifferent++;
				}
				else if(diffOrder) {
					orderDifferent++;
				}
			}
			Console.WriteLine($"Number of Locations: {listLocations.Count}\nDifferent Order: {orderDifferent}\nDifferent Results: {resultsDifferent}");
		}

		///<summary>Uses my original approach to fill a cache of Locations to their n nearest neighbors.
		///This is a O(n^2) operation with as many optimizations as I could think of.</summary>
		private async Task<ConcurrentDictionary<Location, List<Location>>> FillOriginalApproach(List<Location> listLocations, int numberNearestNeighbors) {
			ConcurrentDictionary<Location, List<Location>> retVal = new ConcurrentDictionary<Location, List<Location>>();
			//Prevents us from having to create new coordinates in every loop.
			ConcurrentDictionary<Location, GeoCoordinate> coordinates = new ConcurrentDictionary<Location, GeoCoordinate>();
			foreach(Location loc in listLocations) {
				coordinates[loc] = new GeoCoordinate(loc.Latitude, loc.Longitude);
			}
			List<Task> listTasksToWaitOn = new List<Task>();
			foreach(Location location in listLocations) {
				List<Location> listDeepCopy = new List<Location>(listLocations);
				GeoCoordinate geoCoordinate = coordinates[location];
				//Use Task.Run to force it onto a thread pool thread.
				listTasksToWaitOn.Add(Task.Run(() => {
					List<(double distance, Location l)> listNearestNeighbors = new List<(double distance, Location l)>();
					foreach(Location locCompare in listDeepCopy) {
						if(locCompare == location) {
							continue;
						}
						GeoCoordinate coordinateCompare = coordinates[locCompare];
						double dist = geoCoordinate.GetDistanceTo(coordinateCompare);
						bool wasInserted = false;
						//Using a priority queue would be more efficient here. There is none built in to C# and the performance increase would be marginal.
						for(int i = 0; i < listNearestNeighbors.Count; i++) {
							if(listNearestNeighbors[i].distance > dist) {
								listNearestNeighbors.Insert(i, (dist, locCompare));
								wasInserted = true;
								break;
							}
						}
						if(!wasInserted) {
							listNearestNeighbors.Add((dist, locCompare));
						}
						//If we have exceeded the nearest neighbors, chop off the last one.
						if(listNearestNeighbors.Count > numberNearestNeighbors) {
							listNearestNeighbors.RemoveAt(listNearestNeighbors.Count - 1);
						}
					}
					retVal.TryAdd(location, listNearestNeighbors.Select(x => x.l).ToList());
				}));
			}
			await Task.WhenAll(listTasksToWaitOn);
			return retVal;
		}

		///<summary>Fills the Kd Tree with the passed in locations.</summary>
		private KdTree<double, Location> FillKdTree(List<Location> listLocations) {
			KdTree<double, Location> tree = new KdTree<double, Location>(3, new DoubleMath(), AddDuplicateBehavior.Error);
			//Add each location to the tree by its cartesian coordinates.
			foreach(Location location in listLocations) {
				tree.Add(new double[3] { location.X, location.Y, location.Z }, location);
			}
			return tree;
		}

		///<summary>Returns a random list of n locations.</summary>
		private List<Location> GetRandomLocations(List<Location> listLocations, int n) {
			if(listLocations.Count <= n) {
				return new List<Location>(listLocations);
			}
			HashSet<Location> retVal = new HashSet<Location>();
			while(retVal.Count != n) {
				int index = _random.Next(0, listLocations.Count - 1);
				Location location = listLocations[index];
				//Prevent duplicates.
				if(retVal.Contains(location)) {
					continue;
				}
				retVal.Add(location);
			}
			return retVal.ToList();
		}
	}
}
