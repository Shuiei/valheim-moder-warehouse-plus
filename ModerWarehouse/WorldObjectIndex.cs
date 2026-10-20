using System;
using System.Collections.Generic;

namespace ModerWarehouse;

/// <summary>
/// The world's chests and signs, so the warehouse's world-wide reads do not walk every object in the
/// world. Built from one walk, then kept as objects are created and destroyed; an object created
/// without its prefab yet (one a client sends) is sorted at the next read.
/// </summary>
internal sealed class WorldObjectIndex
{
	private readonly Func<IEnumerable<ZDO>> allObjects;

	private readonly Func<ZDO, bool> isLive;

	private readonly Func<ZDO, bool> isContainer;

	private readonly Func<ZDO, bool> isSign;

	private readonly HashSet<ZDO> containers = new HashSet<ZDO>();

	private readonly HashSet<ZDO> signs = new HashSet<ZDO>();

	private readonly List<ZDO> created = new List<ZDO>();

	private bool built;

	internal WorldObjectIndex(Func<IEnumerable<ZDO>> allObjects, Func<ZDO, bool> isLive, Func<ZDO, bool> isContainer, Func<ZDO, bool> isSign)
	{
		this.allObjects = allObjects;
		this.isLive = isLive;
		this.isContainer = isContainer;
		this.isSign = isSign;
	}

	/// <summary>Whether it hears of created objects; when not, every read walks the world.</summary>
	internal bool FollowsCreations { get; set; } = true;

	internal void Created(ZDO zdo)
	{
		if (built && zdo != null)
		{
			created.Add(zdo);
		}
	}

	internal void Destroyed(ZDO zdo)
	{
		containers.Remove(zdo);
		signs.Remove(zdo);
	}

	/// <summary>The world's chests and signs now, each in id order.</summary>
	internal void Read(List<ZDO> chests, List<ZDO> signObjects)
	{
		if (!built || !FollowsCreations)
		{
			containers.Clear();
			signs.Clear();
			created.Clear();
			foreach (ZDO zdo in allObjects())
			{
				Sort(zdo);
			}
			built = true;
		}
		else
		{
			foreach (ZDO zdo in created)
			{
				if (isLive(zdo))
				{
					Sort(zdo);
				}
			}
			created.Clear();
			containers.RemoveWhere(zdo => !isLive(zdo) || !isContainer(zdo));
			signs.RemoveWhere(zdo => !isLive(zdo) || isContainer(zdo) || !isSign(zdo));
		}
		InIdOrder(containers, chests);
		InIdOrder(signs, signObjects);
	}

	private void Sort(ZDO zdo)
	{
		if (zdo == null)
		{
			return;
		}
		if (isContainer(zdo))
		{
			containers.Add(zdo);
		}
		else if (isSign(zdo))
		{
			signs.Add(zdo);
		}
	}

	private static void InIdOrder(HashSet<ZDO> from, List<ZDO> to)
	{
		to.Clear();
		to.AddRange(from);
		to.Sort((left, right) =>
		{
			int user = left.m_uid.UserID.CompareTo(right.m_uid.UserID);
			return user != 0 ? user : left.m_uid.ID.CompareTo(right.m_uid.ID);
		});
	}
}
