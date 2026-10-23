public static class TurretRules {
 // BL 003 is the Watch Tower. BL 010 is the Workshop; 011 is the Supply Cache.
 public static bool IsTurretID(ulong id){return id==2508874519133192252UL||id==2508879575704982897UL||id==7590277213110165800UL||id==2508881966878207120UL||id==7528194842915619701UL||id==6543199185634881540UL||id==8116859003494801254UL||id==6811496605643591885UL||id==516996878335857867UL;}
 public static bool IsTurret(string name){return name=="ca_f1_turrets"||name=="ca_f1_turrets_classic"||name=="ca_f1_turrets_expansion_classic"||name=="ca_f2_building_003a"||name=="ca_f2_building_003a_classic"||name=="ca_f2_building_003a_expansion_classic"||name=="ca_f3_building_011a"||name=="ca_f3_building_011a_classic"||name=="ca_f3_building_011a_expansion_classic";}
}
