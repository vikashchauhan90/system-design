# Restaurant Finder

```C#

public class Restaurant
{
    public string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}


public class Location
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}


public class RestaurantFinder
{
    private List<Restaurant> _restaurants;

    public RestaurantFinder(List<Restaurant> restaurants)
    {
        _restaurants = restaurants;
    }

    public Restaurant FindNearest(Location currentLocation)
    {
        return _restaurants.OrderBy(r => Distance(currentLocation, r)).First();
    }

    private double Distance(Location currentLocation, Restaurant restaurant)
    {
        var R = 6371e3; // Range (metres)
        var φ1 = currentLocation.Latitude * Math.PI / 180;
        var φ2 = restaurant.Latitude * Math.PI / 180;
        var Δφ = (restaurant.Latitude - currentLocation.Latitude) * Math.PI / 180;
        var Δλ = (restaurant.Longitude - currentLocation.Longitude) * Math.PI / 180;

        var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                Math.Cos(φ1) * Math.Cos(φ2) *
                Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }
}
```