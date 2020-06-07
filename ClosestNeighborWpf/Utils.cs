using System;
using System.Collections.Generic;
using System.IO;

namespace ClosestNeighborWpf {
	public static class Utils {

		///<summary>The radius of the earth in kilometers.</summary>
		public const double RADIUS_OF_EARTH_KM = 6367;

		///<summary>Converts the given longitude and latitude to cartesian coordinates. Assumes that elevation is exactly 0.</summary>
		public static (double x, double y, double z) LongLatToCartesian(double longitude, double latitude) {
			latitude = latitude * (Math.PI / 180);
			longitude = longitude * (Math.PI / 180);
			double x = RADIUS_OF_EARTH_KM * Math.Cos(latitude) * Math.Cos(longitude);
			double y = RADIUS_OF_EARTH_KM * Math.Cos(latitude) * Math.Sin(longitude);
			double z = RADIUS_OF_EARTH_KM * Math.Sin(latitude);
			return (x, y, z);
		}

		///<summary>Reads the .csv file and returns a list of locations contained within.</summary>
		public static List<Location> ReadCSVFile() {
			using StreamReader stream = new StreamReader(@"..\..\..\Data.csv");
			//In case the file has no content.
			if(stream.EndOfStream) {
				return new List<Location>();
			}
			List<Location> listLocations = new List<Location>();
			//Use a HashSet for instance lookup times.
			HashSet<string> addresses = new HashSet<string>();
			HashSet<(double, double)> existingLatLong = new HashSet<(double, double)>();
			//Read the column header line first before data processing.
			_ = stream.ReadLine();
			int count = 1;
			while(!stream.EndOfStream) {
				string row = stream.ReadLine();
				if(row == null) {
					throw new ApplicationException("Shouldn't reach end of stream early.");
				}
				//We want empty columns to be preserved in case an address or any other column is missing.
				string[] columns = row.Split(",", StringSplitOptions.None);
				if(columns.Length < 6) {
					throw new ApplicationException("Incorrect number of columns.");
				}
				if(!double.TryParse(columns[4], out double latitude)) {
					throw new ApplicationException("Incorrect number of columns.");
				}
				if(!double.TryParse(columns[5], out double longitude)) {
					throw new ApplicationException("Bad number of columns.");
				}
				if(existingLatLong.Contains((latitude,longitude))) {
					continue;
				}
				Location locNew = new Location(count, columns[0].Trim(), columns[1].Trim(), columns[2].Trim(), columns[3].Trim(), latitude, longitude);
				string fullAddress = locNew.GetFullAddress();
				//Prevents duplicate addresses. First in wins if the coordinates are different in the different entries.
				if(addresses.Contains(fullAddress)) {
					continue;
				}
				existingLatLong.Add((latitude, longitude));
				addresses.Add(fullAddress);
				listLocations.Add(locNew);
				count++;
			}
			return listLocations;
		}
	}
}
