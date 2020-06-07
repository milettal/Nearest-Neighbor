using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ClosestNeighborWpf {
	public class MainWindowVM : BindableBase {

		///<summary>The delay between retrieving the results and showing them in milliseconds.</summary>
		private const int SEARCH_DELAY_MS = 250;
		///<summary>The number of nearest neighbors to show to the user.</summary>
		private const int NUMBER_NEAREST_NEIGHBORS = 10;
		///<summary>The cache of all processed locations.</summary>
		private LocationCache _cache { get; }
		private CancellationTokenSource _tokenCur { get; set; }

		private Location _selectedLocation;
		///<summary>The location selected by the user.</summary>
		public Location SelectedLocation {
			get => _selectedLocation;
			set => SetProperty(ref _selectedLocation, value);
		}

		private string _searchTerm;
		///<summary>The search term the user has entered.</summary>
		public string SearchTerm {
			get => _searchTerm;
			set => SetProperty(ref _searchTerm, value);
		}

		private List<Location> _listSearchedLocations;
		///<summary>A list of locations that match the search term.</summary>
		public List<Location> ListSearchedLocations {
			get => _listSearchedLocations;
			set { 
				SetProperty(ref _listSearchedLocations, value);
				RaisePropertyChanged(nameof(NumberOfSearchResults));
			}
		}

		private List<Location> _listNearestNeighbors;
		///<summary>A list of the nearest neighbors to the selected location.</summary>
		public List<Location> ListNearestNeighbors {
			get => _listNearestNeighbors;
			set => SetProperty(ref _listNearestNeighbors, value);
		}

		public int NumberOfSearchResults {
			get => ListSearchedLocations?.Count ?? 0;
		}

		public MainWindowVM() {
			_cache = new LocationCache(Utils.ReadCSVFile());
		}

		///<summary>Occurs when the search has been changed.</summary>
		public ICommand SearchChangedCommand => new DelegateCommand(async () => {
			if(string.IsNullOrWhiteSpace(SearchTerm)) {
				_tokenCur?.Cancel();
				_tokenCur = null;
				ListNearestNeighbors = new List<Location>();
				ListSearchedLocations = new List<Location>();
				return;
			}
			List<Location> listResults = null;
			CancellationTokenSource token = new CancellationTokenSource();
			_tokenCur?.Cancel();
			_tokenCur = token;
			//Task.Run to not freeze the main thread.
			await Task.Run(() => {
				listResults = _cache.GetAddressMatches(SearchTerm);
			});
			//Delay a bit, in case the user is typing fast. If they are, the next time this function is called, it will cancel this instance from filling the grid and showing
			//stale results.
			await Task.Delay(SEARCH_DELAY_MS);
			//Check the local instance instead of _tokenCur. _tokenCur could have been overwritten by another instance of this function.
			if(token.IsCancellationRequested) {
				return;
			}
			ListNearestNeighbors = new List<Location>();
			ListSearchedLocations = listResults;
		});

		public ICommand LocationSelectedCommand => new DelegateCommand(() => {
			if(SelectedLocation == null) {
				return;
			}
			ListNearestNeighbors = _cache.GetNearestNeighbor(SelectedLocation, NUMBER_NEAREST_NEIGHBORS);
		});

	}
}
