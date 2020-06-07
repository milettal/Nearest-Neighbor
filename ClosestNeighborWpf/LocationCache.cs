using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using KdTree;
using KdTree.Math;

namespace ClosestNeighborWpf {
	///<summary>Holds all locations read from the file. Handles processing the information for efficient searches and look ups.</summary>
	public class LocationCache {

		///<summary>This is a k-d tree that contains all information about all locations in cartesian coordinates. This has
		///proved to have the same accuracy and far better performance given that our dataset is in the same region. All addresses are either in CO or WY.
		///I tested other methods, such as using an O(n^2) where I calculated the actual distance between every point and sorted them. Even with multithreading the calculations,
		///it did not come near to this method in performance (see unit tests). I got this method through reading the article at:
		///https://www.timvink.nl/closest-coordinates/ among other articles.
		///This should use O(n) space and each search and insertion is O(log n) on average.
		///My preprocessing method would have taken O(n) space, an insertion/preprocessing of O(n^2), and a search of O(1).
		///This solution is far more scalable as the size of data grows.</summary>
		private KdTree<double, Location> _tree = new KdTree<double, Location>(3, new DoubleMath(), AddDuplicateBehavior.Skip);

		///<summary>A cache of previously searched strings. Prevents us from performing the same search twice.
		///Also helps with narrowing our search field.</summary>
		private ConcurrentDictionary<string, List<Location>> _dictionarySearchCache { get; } = new ConcurrentDictionary<string, List<Location>>();

		///<summary>A list of all locations that are sorted by address (sorted just for display purposes).</summary>
		private List<Location> _listLocations { get; }

		public LocationCache(List<Location> listLocations) {
			_listLocations = listLocations
				.OrderBy(x => x.GetFullAddress())
				.ToList();
			//Add each location to the tree by its cartesian coordinates.
			foreach(Location location in _listLocations) {
				_tree.Add(new double[3] { location.X, location.Y, location.Z }, location);
			}
		}

		///<summary>Returns all locations that have matching addresses to the passed in string.
		///This performs in O(n). There may be a more efficient way than looping through all the addresses, but I couldn't think of one.
		///This holds a cache of previously searched terms for instant look ups as well as narrowing down the search space.</summary>
		///<param name="match">The string to find in the addresses.</param>
		public List<Location> GetAddressMatches(string match) {
			if(string.IsNullOrWhiteSpace(match)) {
				return new List<Location>();
			}
			match = match.ToLower();
			if(_dictionarySearchCache.TryGetValue(match, out List<Location> listMatches)) {
				return listMatches;
			}
			//We don't have the exact match in our cache. Let's check each substring, chopping a letter off from the back.
			//This way if the user types abc and then types abcd, we will find abc in our cache and only search through those Locations.
			for(int i = match.Length - 2; i >= 0; i--) {
				string subMatch = match.Substring(0, match.Length - i - 1);
				if(_dictionarySearchCache.TryGetValue(subMatch, out listMatches)) {
					break;
				}
			}
			//If we have no sub matches, we will search the entire location list.
			listMatches ??= _listLocations;
			//Perform the search and fill the cache for this term.
			listMatches = listMatches
				.Where(x => x.Address.Contains(match, StringComparison.OrdinalIgnoreCase))
				.ToList();
			_dictionarySearchCache.TryAdd(match, listMatches);
			return listMatches;
		}

		///<summary>Returns the n nearest locations to the given location. See details from _tree.</summary>
		public List<Location> GetNearestNeighbor(Location location, int n) {
			if(location == null) {
				throw new ArgumentNullException(nameof(location));
			}
			//Grab one more than n as GetNearestNeighbours will always return the location itself
			return _tree.GetNearestNeighbours(new double[3] { location.X, location.Y, location.Z }, n + 1)
				.Where(x => x.Value != location)
				.Select(x => x.Value)
				.ToList();
		}
	}
}
