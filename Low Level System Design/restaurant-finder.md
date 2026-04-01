# Restaurant Finder

```C#

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class Location
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; }
    
    public Location() { }
    
    public Location(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
    
    public Location(double latitude, double longitude, string address)
    {
        Latitude = latitude;
        Longitude = longitude;
        Address = address;
    }
    
    public override string ToString()
    {
        return string.IsNullOrEmpty(Address) 
            ? $"Lat: {Latitude:F6}, Lon: {Longitude:F6}"
            : $"{Address} ({Latitude:F6}, {Longitude:F6})";
    }
    
    // Calculate distance to another location
    public double DistanceTo(Location other, DistanceUnit unit = DistanceUnit.Kilometers)
    {
        if (other == null) return 0;
        return LocationCalculator.CalculateDistance(this, other, unit);
    }
}

public class Restaurant
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Location Location { get; set; }  // Integrated Location class
    public string CuisineType { get; set; }
    public double Rating { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string Website { get; set; }
    public bool IsOpen { get; set; }
    public TimeSpan OpeningTime { get; set; }
    public TimeSpan ClosingTime { get; set; }
    public List<string> Amenities { get; set; }
    public PriceRange PriceRange { get; set; }
    public int ReviewCount { get; set; }
    public DateTime LastUpdated { get; set; }
    
    public Restaurant()
    {
        Id = Guid.NewGuid().ToString();
        Amenities = new List<string>();
        PriceRange = PriceRange.Medium;
        LastUpdated = DateTime.UtcNow;
    }
    
    public bool IsOpenNow()
    {
        if (!IsOpen) return false;
        var currentTime = DateTime.Now.TimeOfDay;
        return currentTime >= OpeningTime && currentTime <= ClosingTime;
    }
    
    public override string ToString()
    {
        return $"{Name} - {CuisineType} - Rating: {Rating}★ - {Location}";
    }
}

public enum PriceRange
{
    Inexpensive = 1,
    Medium = 2,
    Expensive = 3,
    Luxury = 4
}

public enum DistanceUnit
{
    Kilometers,
    Meters,
    Miles
}

public class SearchCriteria
{
    public Location CurrentLocation { get; set; }
    public double MaxDistanceKm { get; set; } = double.MaxValue;
    public string CuisineType { get; set; }
    public double MinRating { get; set; } = 0;
    public bool OnlyOpenNow { get; set; } = false;
    public PriceRange? MaxPriceRange { get; set; }
    public int MaxResults { get; set; } = 10;
    public string SearchTerm { get; set; }
    public List<string> Amenities { get; set; }
    public SortBy SortBy { get; set; } = SortBy.Distance;
    public SortOrder SortOrder { get; set; } = SortOrder.Ascending;
}

public enum SortBy
{
    Distance,
    Rating,
    Price,
    Name,
    Relevance
}

public enum SortOrder
{
    Ascending,
    Descending
}

public class RestaurantResult
{
    public Restaurant Restaurant { get; set; }
    public double DistanceKm { get; set; }
    public double DistanceMiles { get; set; }
    public double Bearing { get; set; }
    public string BearingDirection { get; set; }
    public int Rank { get; set; }
    
    public override string ToString()
    {
        return $"{Restaurant.Name} - {DistanceKm:F2}km away - {Restaurant.Rating}★";
    }
}

public class RestaurantFinder
{
    private List<Restaurant> _restaurants;
    private readonly object _lock = new object();
    private Dictionary<string, List<Restaurant>> _cuisineIndex;
    private Dictionary<double, List<Restaurant>> _ratingIndex;
    private Dictionary<PriceRange, List<Restaurant>> _priceIndex;
    private readonly Dictionary<string, List<RestaurantResult>> _searchCache;
    
    public RestaurantFinder(List<Restaurant> restaurants = null)
    {
        _restaurants = restaurants ?? new List<Restaurant>();
        _cuisineIndex = new Dictionary<string, List<Restaurant>>();
        _ratingIndex = new Dictionary<double, List<Restaurant>>();
        _priceIndex = new Dictionary<PriceRange, List<Restaurant>>();
        _searchCache = new Dictionary<string, List<RestaurantResult>>();
        
        BuildIndexes();
    }
    
    // Build indexes for faster searches
    private void BuildIndexes()
    {
        _cuisineIndex.Clear();
        _ratingIndex.Clear();
        _priceIndex.Clear();
        
        foreach (var restaurant in _restaurants)
        {
            // Cuisine index
            if (!string.IsNullOrEmpty(restaurant.CuisineType))
            {
                var cuisine = restaurant.CuisineType.ToLowerInvariant();
                if (!_cuisineIndex.ContainsKey(cuisine))
                    _cuisineIndex[cuisine] = new List<Restaurant>();
                _cuisineIndex[cuisine].Add(restaurant);
            }
            
            // Rating index (rounded to nearest 0.5 for grouping)
            var ratingKey = Math.Round(restaurant.Rating * 2) / 2;
            if (!_ratingIndex.ContainsKey(ratingKey))
                _ratingIndex[ratingKey] = new List<Restaurant>();
            _ratingIndex[ratingKey].Add(restaurant);
            
            // Price index
            if (!_priceIndex.ContainsKey(restaurant.PriceRange))
                _priceIndex[restaurant.PriceRange] = new List<Restaurant>();
            _priceIndex[restaurant.PriceRange].Add(restaurant);
        }
    }
    
    /// <summary>
    /// Find nearest restaurants (returns list instead of single)
    /// </summary>
    public List<RestaurantResult> FindNearestRestaurants(
        Location currentLocation, 
        int count = 10, 
        DistanceUnit unit = DistanceUnit.Kilometers)
    {
        if (currentLocation == null)
            throw new ArgumentNullException(nameof(currentLocation));
        
        if (!_restaurants.Any())
            return new List<RestaurantResult>();
        
        // Calculate distances and create results
        var results = _restaurants
            .Select(restaurant => new RestaurantResult
            {
                Restaurant = restaurant,
                DistanceKm = LocationCalculator.CalculateDistance(currentLocation, restaurant.Location, DistanceUnit.Kilometers),
                DistanceMiles = LocationCalculator.CalculateDistance(currentLocation, restaurant.Location, DistanceUnit.Miles),
                Bearing = LocationCalculator.CalculateBearing(currentLocation, restaurant.Location),
                BearingDirection = GetBearingDirection(LocationCalculator.CalculateBearing(currentLocation, restaurant.Location))
            })
            .OrderBy(r => r.DistanceKm)
            .Take(count)
            .ToList();
        
        // Add rank
        for (int i = 0; i < results.Count; i++)
        {
            results[i].Rank = i + 1;
        }
        
        return results;
    }
    
    /// <summary>
    /// Search restaurants with advanced criteria (returns list)
    /// </summary>
    public List<RestaurantResult> SearchRestaurants(SearchCriteria criteria)
    {
        if (criteria == null)
            throw new ArgumentNullException(nameof(criteria));
        
        // Generate cache key for similar searches
        var cacheKey = GenerateCacheKey(criteria);
        
        lock (_lock)
        {
            if (_searchCache.TryGetValue(cacheKey, out var cachedResults))
                return cachedResults;
        }
        
        var query = _restaurants.AsEnumerable();
        
        // Apply cuisine filter
        if (!string.IsNullOrEmpty(criteria.CuisineType))
        {
            var cuisineLower = criteria.CuisineType.ToLowerInvariant();
            if (_cuisineIndex.ContainsKey(cuisineLower))
                query = _cuisineIndex[cuisineLower];
            else
                return new List<RestaurantResult>();
        }
        
        // Apply rating filter
        if (criteria.MinRating > 0)
        {
            query = query.Where(r => r.Rating >= criteria.MinRating);
        }
        
        // Apply price filter
        if (criteria.MaxPriceRange.HasValue)
        {
            query = query.Where(r => (int)r.PriceRange <= (int)criteria.MaxPriceRange.Value);
        }
        
        // Apply open now filter
        if (criteria.OnlyOpenNow)
        {
            query = query.Where(r => r.IsOpenNow());
        }
        
        // Apply search term filter
        if (!string.IsNullOrEmpty(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.ToLowerInvariant();
            query = query.Where(r => 
                r.Name.ToLowerInvariant().Contains(term) ||
                r.CuisineType.ToLowerInvariant().Contains(term) ||
                (r.Location.Address?.ToLowerInvariant().Contains(term) ?? false));
        }
        
        // Apply amenities filter
        if (criteria.Amenities != null && criteria.Amenities.Any())
        {
            query = query.Where(r => 
                criteria.Amenities.All(a => r.Amenities.Contains(a, StringComparer.OrdinalIgnoreCase)));
        }
        
        // Calculate distances if location is provided
        List<RestaurantResult> results;
        
        if (criteria.CurrentLocation != null)
        {
            results = query
                .Select(restaurant => new RestaurantResult
                {
                    Restaurant = restaurant,
                    DistanceKm = LocationCalculator.CalculateDistance(
                        criteria.CurrentLocation, 
                        restaurant.Location, 
                        DistanceUnit.Kilometers),
                    DistanceMiles = LocationCalculator.CalculateDistance(
                        criteria.CurrentLocation, 
                        restaurant.Location, 
                        DistanceUnit.Miles),
                    Bearing = LocationCalculator.CalculateBearing(
                        criteria.CurrentLocation, 
                        restaurant.Location),
                    BearingDirection = GetBearingDirection(
                        LocationCalculator.CalculateBearing(criteria.CurrentLocation, restaurant.Location))
                })
                .ToList();
            
            // Apply distance filter
            if (criteria.MaxDistanceKm < double.MaxValue)
            {
                results = results.Where(r => r.DistanceKm <= criteria.MaxDistanceKm).ToList();
            }
        }
        else
        {
            results = query
                .Select(restaurant => new RestaurantResult
                {
                    Restaurant = restaurant,
                    DistanceKm = -1,
                    DistanceMiles = -1
                })
                .ToList();
        }
        
        // Apply sorting
        results = ApplySorting(results, criteria);
        
        // Apply max results limit
        if (criteria.MaxResults > 0)
        {
            results = results.Take(criteria.MaxResults).ToList();
        }
        
        // Add rank
        for (int i = 0; i < results.Count; i++)
        {
            results[i].Rank = i + 1;
        }
        
        // Cache results
        lock (_lock)
        {
            _searchCache[cacheKey] = results;
            
            // Limit cache size
            if (_searchCache.Count > 100)
            {
                var oldestKey = _searchCache.Keys.First();
                _searchCache.Remove(oldestKey);
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Find restaurants by cuisine type (returns list)
    /// </summary>
    public List<Restaurant> FindByCuisine(string cuisineType)
    {
        if (string.IsNullOrEmpty(cuisineType))
            return new List<Restaurant>();
        
        var cuisineLower = cuisineType.ToLowerInvariant();
        return _cuisineIndex.TryGetValue(cuisineLower, out var restaurants) 
            ? restaurants.ToList() 
            : new List<Restaurant>();
    }
    
    /// <summary>
    /// Find restaurants by rating range (returns list)
    /// </summary>
    public List<Restaurant> FindByRatingRange(double minRating, double maxRating)
    {
        return _restaurants
            .Where(r => r.Rating >= minRating && r.Rating <= maxRating)
            .OrderByDescending(r => r.Rating)
            .ToList();
    }
    
    /// <summary>
    /// Find restaurants by price range (returns list)
    /// </summary>
    public List<Restaurant> FindByPriceRange(PriceRange minPrice, PriceRange maxPrice)
    {
        return _restaurants
            .Where(r => r.PriceRange >= minPrice && r.PriceRange <= maxPrice)
            .OrderBy(r => r.PriceRange)
            .ToList();
    }
    
    /// <summary>
    /// Find restaurants within a radius (returns list)
    /// </summary>
    public List<RestaurantResult> FindWithinRadius(
        Location centerLocation, 
        double radius, 
        DistanceUnit unit = DistanceUnit.Kilometers)
    {
        if (centerLocation == null)
            throw new ArgumentNullException(nameof(centerLocation));
        
        return _restaurants
            .Select(restaurant => new RestaurantResult
            {
                Restaurant = restaurant,
                DistanceKm = LocationCalculator.CalculateDistance(centerLocation, restaurant.Location, DistanceUnit.Kilometers),
                DistanceMiles = LocationCalculator.CalculateDistance(centerLocation, restaurant.Location, DistanceUnit.Miles),
                Bearing = LocationCalculator.CalculateBearing(centerLocation, restaurant.Location),
                BearingDirection = GetBearingDirection(LocationCalculator.CalculateBearing(centerLocation, restaurant.Location))
            })
            .Where(r => r.DistanceKm <= (unit == DistanceUnit.Miles ? radius * 1.60934 : radius))
            .OrderBy(r => r.DistanceKm)
            .ToList();
    }
    
    /// <summary>
    /// Get nearby restaurants with rich information (returns list)
    /// </summary>
    public async Task<List<NearbyRestaurantInfo>> GetNearbyRestaurantsInfoAsync(
        Location currentLocation, 
        double radiusKm = 5,
        int maxResults = 20)
    {
        return await Task.Run(() =>
        {
            var nearby = FindWithinRadius(currentLocation, radiusKm, DistanceUnit.Kilometers)
                .Take(maxResults)
                .ToList();
            
            return nearby.Select(r => new NearbyRestaurantInfo
            {
                Restaurant = r.Restaurant,
                DistanceKm = r.DistanceKm,
                EstimatedTravelTimeMinutes = CalculateTravelTime(r.DistanceKm, TravelMode.Driving),
                BearingDirection = r.BearingDirection,
                IsOpenNow = r.Restaurant.IsOpenNow(),
                PopularityScore = CalculatePopularityScore(r.Restaurant),
                RecommendationReason = GetRecommendationReason(r.Restaurant, r.DistanceKm)
            }).ToList();
        });
    }
    
    /// <summary>
    /// Group restaurants by distance bands (returns grouped list)
    /// </summary>
    public Dictionary<string, List<RestaurantResult>> GroupByDistanceBands(
        Location currentLocation, 
        double[] distanceBandsKm)
    {
        if (currentLocation == null)
            throw new ArgumentNullException(nameof(currentLocation));
        
        var results = new Dictionary<string, List<RestaurantResult>>();
        
        // Initialize groups
        foreach (var band in distanceBandsKm.OrderBy(b => b))
        {
            results[$"Within {band} km"] = new List<RestaurantResult>();
        }
        results["Beyond"] = new List<RestaurantResult>();
        
        // Calculate distances and group
        var allResults = _restaurants
            .Select(restaurant => new RestaurantResult
            {
                Restaurant = restaurant,
                DistanceKm = LocationCalculator.CalculateDistance(currentLocation, restaurant.Location, DistanceUnit.Kilometers),
                DistanceMiles = LocationCalculator.CalculateDistance(currentLocation, restaurant.Location, DistanceUnit.Miles)
            })
            .ToList();
        
        foreach (var result in allResults)
        {
            bool placed = false;
            
            foreach (var band in distanceBandsKm.OrderBy(b => b))
            {
                if (result.DistanceKm <= band)
                {
                    results[$"Within {band} km"].Add(result);
                    placed = true;
                    break;
                }
            }
            
            if (!placed)
            {
                results["Beyond"].Add(result);
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Get restaurant statistics
    /// </summary>
    public RestaurantStatistics GetStatistics()
    {
        return new RestaurantStatistics
        {
            TotalRestaurants = _restaurants.Count,
            AverageRating = _restaurants.Any() ? _restaurants.Average(r => r.Rating) : 0,
            CuisineTypes = _cuisineIndex.Keys.Count,
            PriceRangeDistribution = _priceIndex.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.Count),
            TopRatedRestaurants = _restaurants
                .OrderByDescending(r => r.Rating)
                .Take(5)
                .Select(r => new TopRestaurant { Name = r.Name, Rating = r.Rating })
                .ToList()
        };
    }
    
    // Helper methods
    private List<RestaurantResult> ApplySorting(List<RestaurantResult> results, SearchCriteria criteria)
    {
        return criteria.SortBy switch
        {
            SortBy.Distance => criteria.SortOrder == SortOrder.Ascending
                ? results.OrderBy(r => r.DistanceKm).ToList()
                : results.OrderByDescending(r => r.DistanceKm).ToList(),
                
            SortBy.Rating => criteria.SortOrder == SortOrder.Ascending
                ? results.OrderBy(r => r.Restaurant.Rating).ToList()
                : results.OrderByDescending(r => r.Restaurant.Rating).ToList(),
                
            SortBy.Price => criteria.SortOrder == SortOrder.Ascending
                ? results.OrderBy(r => r.Restaurant.PriceRange).ToList()
                : results.OrderByDescending(r => r.Restaurant.PriceRange).ToList(),
                
            SortBy.Name => criteria.SortOrder == SortOrder.Ascending
                ? results.OrderBy(r => r.Restaurant.Name).ToList()
                : results.OrderByDescending(r => r.Restaurant.Name).ToList(),
                
            _ => results
        };
    }
    
    private string GenerateCacheKey(SearchCriteria criteria)
    {
        return $"{criteria.CuisineType}_{criteria.MinRating}_{criteria.MaxPriceRange}_{criteria.OnlyOpenNow}_{criteria.SearchTerm}";
    }
    
    private static string GetBearingDirection(double bearing)
    {
        string[] directions = { "North", "Northeast", "East", "Southeast", "South", "Southwest", "West", "Northwest" };
        int index = (int)Math.Round(bearing / 45) % 8;
        return directions[index];
    }
    
    private static double CalculateTravelTime(double distanceKm, TravelMode mode)
    {
        // Average speeds in km/h
        double speedKmh = mode switch
        {
            TravelMode.Walking => 5,
            TravelMode.Biking => 15,
            TravelMode.Driving => 40,
            TravelMode.Transit => 25,
            _ => 40
        };
        
        return (distanceKm / speedKmh) * 60; // Convert to minutes
    }
    
    private static double CalculatePopularityScore(Restaurant restaurant)
    {
        // Combine rating and review count for popularity score
        double ratingWeight = 0.7;
        double reviewWeight = 0.3;
        
        double normalizedRating = restaurant.Rating / 5.0;
        double normalizedReviews = Math.Min(restaurant.ReviewCount / 1000.0, 1.0);
        
        return (normalizedRating * ratingWeight + normalizedReviews * reviewWeight) * 100;
    }
    
    private static string GetRecommendationReason(Restaurant restaurant, double distanceKm)
    {
        if (restaurant.Rating >= 4.5)
            return "Top rated restaurant in the area";
        if (distanceKm <= 1)
            return "Very close to your location";
        if (restaurant.IsOpenNow())
            return "Currently open";
        if (restaurant.PriceRange == PriceRange.Inexpensive)
            return "Budget-friendly option";
        
        return "Recommended based on your preferences";
    }
    
    // CRUD operations
    public void AddRestaurant(Restaurant restaurant)
    {
        if (restaurant == null)
            throw new ArgumentNullException(nameof(restaurant));
        
        lock (_lock)
        {
            _restaurants.Add(restaurant);
            BuildIndexes(); // Rebuild indexes
            _searchCache.Clear(); // Clear cache
        }
    }
    
    public void AddRestaurants(List<Restaurant> restaurants)
    {
        if (restaurants == null)
            throw new ArgumentNullException(nameof(restaurants));
        
        lock (_lock)
        {
            _restaurants.AddRange(restaurants);
            BuildIndexes();
            _searchCache.Clear();
        }
    }
    
    public bool RemoveRestaurant(string restaurantId)
    {
        lock (_lock)
        {
            var restaurant = _restaurants.FirstOrDefault(r => r.Id == restaurantId);
            if (restaurant != null)
            {
                _restaurants.Remove(restaurant);
                BuildIndexes();
                _searchCache.Clear();
                return true;
            }
            return false;
        }
    }
    
    public void UpdateRestaurant(Restaurant updatedRestaurant)
    {
        lock (_lock)
        {
            var index = _restaurants.FindIndex(r => r.Id == updatedRestaurant.Id);
            if (index >= 0)
            {
                _restaurants[index] = updatedRestaurant;
                BuildIndexes();
                _searchCache.Clear();
            }
        }
    }
    
    public void ClearCache()
    {
        lock (_lock)
        {
            _searchCache.Clear();
        }
    }
    
    public List<Restaurant> GetAllRestaurants()
    {
        return _restaurants.ToList();
    }
}

// Additional supporting classes
public static class LocationCalculator
{
    private const double EarthRadiusKilometers = 6371;
    private const double EarthRadiusMeters = 6371000;
    private const double EarthRadiusMiles = 3959;
    
    public static double CalculateDistance(Location location1, Location location2, DistanceUnit unit = DistanceUnit.Kilometers)
    {
        if (location1 == null || location2 == null)
            return 0;
        
        double lat1Rad = DegreesToRadians(location1.Latitude);
        double lon1Rad = DegreesToRadians(location1.Longitude);
        double lat2Rad = DegreesToRadians(location2.Latitude);
        double lon2Rad = DegreesToRadians(location2.Longitude);
        
        double deltaLat = lat2Rad - lat1Rad;
        double deltaLon = lon2Rad - lon1Rad;
        
        double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                   Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        double distanceKm = EarthRadiusKilometers * c;
        
        return unit switch
        {
            DistanceUnit.Meters => distanceKm * 1000,
            DistanceUnit.Miles => distanceKm * 0.621371,
            _ => distanceKm
        };
    }
    
    public static double CalculateBearing(Location location1, Location location2)
    {
        if (location1 == null || location2 == null)
            return 0;
        
        double lat1Rad = DegreesToRadians(location1.Latitude);
        double lat2Rad = DegreesToRadians(location2.Latitude);
        double lonDiffRad = DegreesToRadians(location2.Longitude - location1.Longitude);
        
        double y = Math.Sin(lonDiffRad) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                   Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(lonDiffRad);
        
        double bearing = Math.Atan2(y, x);
        bearing = bearing * 180 / Math.PI;
        bearing = (bearing + 360) % 360;
        
        return bearing;
    }
    
    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}

public class NearbyRestaurantInfo
{
    public Restaurant Restaurant { get; set; }
    public double DistanceKm { get; set; }
    public double EstimatedTravelTimeMinutes { get; set; }
    public string BearingDirection { get; set; }
    public bool IsOpenNow { get; set; }
    public double PopularityScore { get; set; }
    public string RecommendationReason { get; set; }
}

public class RestaurantStatistics
{
    public int TotalRestaurants { get; set; }
    public double AverageRating { get; set; }
    public int CuisineTypes { get; set; }
    public Dictionary<PriceRange, int> PriceRangeDistribution { get; set; }
    public List<TopRestaurant> TopRatedRestaurants { get; set; }
}

public class TopRestaurant
{
    public string Name { get; set; }
    public double Rating { get; set; }
}

public enum TravelMode
{
    Walking,
    Biking,
    Driving,
    Transit
}
```