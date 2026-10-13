namespace ModerWarehouse;

public static class WarehouseDistancePolicy
{
	public static bool IsTooClose(float distance, float minimum)
	{
		return distance < minimum;
	}
}
