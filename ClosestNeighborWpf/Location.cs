namespace ClosestNeighborWpf {

	///<summary>Represents a single position on the globe via name (address) and position (latitude and longitude).</summary>
	public class Location {

		public long Id { get; }

		public string Address { get; }

		public string City { get; }

		public string State { get; }

		public string Zip { get; }

		public double Latitude { get; }

		public double Longitude { get; }

		public double X { get; }

		public double Y { get; }

		public double Z { get; }

		public Location(long id, string address, string city, string state, string zip, double latitude, double longitude) {
			Address = address;
			City = city;
			State = state;
			Zip = zip;
			Latitude = latitude;
			Longitude = longitude;
			(double x, double y, double z) = Utils.LongLatToCartesian(Longitude, Latitude);
			X = x;
			Y = y;
			Z = z;
			Id = id;
		}

		///<summary>Returns the full address of this location.</summary>
		public string GetFullAddress() {
			return $"{Address} {City}, {State}, {Zip}";
		}

	}

}
