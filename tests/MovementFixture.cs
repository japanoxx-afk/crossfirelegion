namespace Carbon.Core.Numerics.Q64 {
 public struct Vector2 {
  public int X,Y;public static readonly Vector2 Zero=new Vector2();public Vector2(int x,int y){X=x;Y=y;}
  public static bool operator !=(Vector2 a,Vector2 b){return a.X!=b.X || a.Y!=b.Y;}public static bool operator ==(Vector2 a,Vector2 b){return !(a!=b);}public override bool Equals(object value){return value is Vector2 && this==(Vector2)value;}public override int GetHashCode(){return X^Y;}
 }
}
namespace Carbon.Simulation {
 public class MovementUnit {public bool Friendly;public int X,Y;}
 public enum Layer {Ground=1,Air=2,Other=4}
 public class RTSCollision {public Layer ActiveCollisionLayer;}
 public static class VelocityComponent {public static Carbon.Core.Numerics.Q64.Vector2 GetVelocity2(MovementUnit unit){return new Carbon.Core.Numerics.Q64.Vector2(unit.X,unit.Y);}}
 public static class YieldHelpers {
  public static bool EntityPairIgnoreCollision(MovementUnit first,MovementUnit second){return EntityPairIgnoreCollision(first,second,first,second);}
  public static bool EntityPairIgnoreCollision(MovementUnit first,MovementUnit second,MovementUnit ownerFirst,MovementUnit ownerSecond){return first.Friendly && second.Friendly && (VelocityComponent.GetVelocity2(first)!=Carbon.Core.Numerics.Q64.Vector2.Zero || VelocityComponent.GetVelocity2(second)!=Carbon.Core.Numerics.Q64.Vector2.Zero);}
 }
}
