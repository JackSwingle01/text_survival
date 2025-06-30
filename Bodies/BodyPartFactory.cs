namespace text_survival.Bodies;

public static class BodyPartFactory
{
    public enum BodyTypes
    {
        Human,
        Quadruped,
        Serpentine,
        Arachnid,
        Flying
    }

    public static List<BodyRegion> CreateBody(BodyTypes type)
    {
        return type switch
        {
            BodyTypes.Human => CreateHumanBody(),
            BodyTypes.Quadruped => CreateQuadrupedBody(),
            BodyTypes.Serpentine => CreateSerpentineBody(),
            BodyTypes.Arachnid => CreateArachnidBody(),
            BodyTypes.Flying => CreateFlyingBody(),
            _ => throw new NotImplementedException("Invalid body type")
        };
    }

    public static List<BodyRegion> CreateHumanBody()
    {
        var parts = new List<BodyRegion>();

        // HEAD - 10% coverage
        var head = new BodyRegion(BodyRegionNames.Head, 10.0);
        // Skull is very tough (20), but head overall is average
        head.Bone.Toughness = 20;
        
        // Brain - controls consciousness, very fragile
        head.Organs.Add(new Organ(OrganNames.Brain, 0.25, new CapacityContainer 
        { 
            Consciousness = 1.0 
        }, isExternal: false));

        // Eyes - each provides half sight, fragile, external
        head.Organs.Add(new Organ(OrganNames.LeftEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.5 
        }, isExternal: true));
        
        head.Organs.Add(new Organ(OrganNames.RightEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.5 
        }, isExternal: true));

        // Ears - each provides half hearing, moderately fragile, external
        head.Organs.Add(new Organ(OrganNames.LeftEar, 1.0, new CapacityContainer 
        { 
            Hearing = 0.5 
        }, isExternal: true));
        
        head.Organs.Add(new Organ(OrganNames.RightEar, 1.0, new CapacityContainer 
        { 
            Hearing = 0.5 
        }, isExternal: true));

        parts.Add(head);

        // CHEST - 25% coverage
        var chest = new BodyRegion(BodyRegionNames.Chest, 25.0);
        // Ribcage protection
        chest.Bone.Toughness = 12;

        // Heart - critical for blood pumping, moderately tough
        chest.Organs.Add(new Organ(OrganNames.Heart, 5.0, new CapacityContainer 
        { 
            BloodPumping = 1.0 
        }, isExternal: false));

        // Lungs - each provides half breathing capacity, somewhat fragile
        chest.Organs.Add(new Organ(OrganNames.LeftLung, 3.0, new CapacityContainer 
        { 
            Breathing = 0.5 
        }, isExternal: false));
        
        chest.Organs.Add(new Organ(OrganNames.RightLung, 3.0, new CapacityContainer 
        { 
            Breathing = 0.5 
        }, isExternal: false));

        parts.Add(chest);

        // ABDOMEN - 20% coverage
        var abdomen = new BodyRegion(BodyRegionNames.Abdomen, 20.0);
        
        // Liver - major digestive organ, moderately tough
        abdomen.Organs.Add(new Organ(OrganNames.Liver, 4.0, new CapacityContainer 
        { 
            Digestion = 0.6 
        }, isExternal: false));

        // Stomach - digestive organ, somewhat fragile
        abdomen.Organs.Add(new Organ(OrganNames.Stomach, 3.0, new CapacityContainer 
        { 
            Digestion = 0.4 
        }, isExternal: false));

        // Kidneys - redundant organs, each can handle most of the load
        abdomen.Organs.Add(new Organ("Left Kidney", 4.0, new CapacityContainer 
        { 
            // BloodFiltration = 0.75 - would need to add this capacity
        }, isExternal: false));
        
        abdomen.Organs.Add(new Organ("Right Kidney", 4.0, new CapacityContainer 
        { 
            // BloodFiltration = 0.75 - would need to add this capacity
        }, isExternal: false));

        parts.Add(abdomen);

        // LEFT ARM - 10% coverage
        var leftArm = new BodyRegion(BodyRegionNames.LeftArm, 10.0);
        // No specific organs, but contributes to manipulation through muscle/bone
        // The base capacities could be set on the part itself or through muscle
        parts.Add(leftArm);

        // RIGHT ARM - 10% coverage  
        var rightArm = new BodyRegion(BodyRegionNames.RightArm, 10.0);
        parts.Add(rightArm);

        // LEFT LEG - 12.5% coverage
        var leftLeg = new BodyRegion(BodyRegionNames.LeftLeg, 12.5);
        parts.Add(leftLeg);

        // RIGHT LEG - 12.5% coverage
        var rightLeg = new BodyRegion(BodyRegionNames.RightLeg, 12.5);
        parts.Add(rightLeg);

        return parts;
    }

    public static List<BodyRegion> CreateQuadrupedBody()
    {
        var parts = new List<BodyRegion>();

        // HEAD - 15% coverage (proportionally larger for quadrupeds)
        var head = new BodyRegion("Head", 15.0);
        head.Bone.Toughness = 15; // Slightly less protected than human skull
        
        // Brain
        head.Organs.Add(new Organ(OrganNames.Brain, 0.25, new CapacityContainer 
        { 
            Consciousness = 1.0 
        }, isExternal: false));

        // Eyes - better sight for predators/prey
        head.Organs.Add(new Organ(OrganNames.LeftEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.6 
        }, isExternal: true));
        
        head.Organs.Add(new Organ(OrganNames.RightEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.6 
        }, isExternal: true));

        // Ears - better hearing
        head.Organs.Add(new Organ(OrganNames.LeftEar, 1.0, new CapacityContainer 
        { 
            Hearing = 0.6 
        }, isExternal: true));
        
        head.Organs.Add(new Organ(OrganNames.RightEar, 1.0, new CapacityContainer 
        { 
            Hearing = 0.6 
        }, isExternal: true));

        parts.Add(head);

        // TORSO - 35% coverage (larger torso for quadrupeds)
        var torso = new BodyRegion("Torso", 35.0);
        
        // Heart
        torso.Organs.Add(new Organ(OrganNames.Heart, 5.0, new CapacityContainer 
        { 
            BloodPumping = 1.0 
        }, isExternal: false));

        // Lungs
        torso.Organs.Add(new Organ(OrganNames.LeftLung, 3.0, new CapacityContainer 
        { 
            Breathing = 0.5 
        }, isExternal: false));
        
        torso.Organs.Add(new Organ(OrganNames.RightLung, 3.0, new CapacityContainer 
        { 
            Breathing = 0.5 
        }, isExternal: false));

        // Digestive organs
        torso.Organs.Add(new Organ(OrganNames.Liver, 4.0, new CapacityContainer 
        { 
            Digestion = 0.6 
        }, isExternal: false));

        torso.Organs.Add(new Organ(OrganNames.Stomach, 3.0, new CapacityContainer 
        { 
            Digestion = 0.4 
        }, isExternal: false));

        parts.Add(torso);

        // LEGS - 12.5% each for four legs
        var frontLeftLeg = new BodyRegion("Front Left Leg", 12.5);
        var frontRightLeg = new BodyRegion("Front Right Leg", 12.5);
        var rearLeftLeg = new BodyRegion("Rear Left Leg", 12.5);
        var rearRightLeg = new BodyRegion("Rear Right Leg", 12.5);

        parts.AddRange([frontLeftLeg, frontRightLeg, rearLeftLeg, rearRightLeg]);

        return parts;
    }

    public static List<BodyRegion> CreateSerpentineBody()
    {
        var parts = new List<BodyRegion>();

        // HEAD - 20% coverage
        var head = new BodyRegion("Head", 20.0);
        head.Bone.Toughness = 8; // Less protected than mammalian skulls
        
        // Brain
        head.Organs.Add(new Organ(OrganNames.Brain, 0.25, new CapacityContainer 
        { 
            Consciousness = 1.0 
        }, isExternal: false));

        // Eyes
        head.Organs.Add(new Organ(OrganNames.LeftEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.5 
        }, isExternal: true));
        
        head.Organs.Add(new Organ(OrganNames.RightEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.5 
        }, isExternal: true));

        parts.Add(head);

        // BODY - 80% coverage (long serpentine body)
        var body = new BodyRegion("Body", 80.0);
        
        // Heart
        body.Organs.Add(new Organ(OrganNames.Heart, 4.0, new CapacityContainer 
        { 
            BloodPumping = 1.0 
        }, isExternal: false));

        // Single lung (snakes typically have one functional lung)
        body.Organs.Add(new Organ("Lung", 3.0, new CapacityContainer 
        { 
            Breathing = 1.0 
        }, isExternal: false));

        // Digestive organs
        body.Organs.Add(new Organ(OrganNames.Liver, 4.0, new CapacityContainer 
        { 
            Digestion = 0.6 
        }, isExternal: false));

        body.Organs.Add(new Organ(OrganNames.Stomach, 3.0, new CapacityContainer 
        { 
            Digestion = 0.4 
        }, isExternal: false));

        parts.Add(body);

        return parts;
    }

    public static List<BodyRegion> CreateArachnidBody()
    {
        var parts = new List<BodyRegion>();

        // CEPHALOTHORAX - 40% coverage (head and thorax combined)
        var cephalothorax = new BodyRegion("Cephalothorax", 40.0);
        cephalothorax.Bone.Toughness = 12; // Chitin is quite tough
        
        // Brain
        cephalothorax.Organs.Add(new Organ(OrganNames.Brain, 0.3, new CapacityContainer 
        { 
            Consciousness = 1.0 
        }, isExternal: false));

        // Multiple eyes - spiders typically have 8 eyes
        for (int i = 1; i <= 8; i++)
        {
            cephalothorax.Organs.Add(new Organ($"Eye {i}", 0.2, new CapacityContainer 
            { 
                Sight = 0.125 // Each contributes 1/8th of total sight
            }, isExternal: true));
        }

        // Heart
        cephalothorax.Organs.Add(new Organ(OrganNames.Heart, 3.0, new CapacityContainer 
        { 
            BloodPumping = 1.0 
        }, isExternal: false));

        parts.Add(cephalothorax);

        // ABDOMEN - 20% coverage
        var abdomen = new BodyRegion("Abdomen", 20.0);
        
        // Book lungs (spiders have book lungs instead of regular lungs)
        abdomen.Organs.Add(new Organ("Book Lungs", 2.0, new CapacityContainer 
        { 
            Breathing = 1.0 
        }, isExternal: false));

        // Digestive organs
        abdomen.Organs.Add(new Organ("Digestive System", 3.0, new CapacityContainer 
        { 
            Digestion = 1.0 
        }, isExternal: false));

        parts.Add(abdomen);

        // LEGS - 8 legs, 5% coverage each
        for (int i = 1; i <= 8; i++)
        {
            var leg = new BodyRegion($"Leg {i}", 5.0);
            parts.Add(leg);
        }

        return parts;
    }

    public static List<BodyRegion> CreateFlyingBody()
    {
        var parts = new List<BodyRegion>();

        // HEAD - 12% coverage
        var head = new BodyRegion("Head", 12.0);
        head.Bone.Toughness = 8; // Lighter bones for flight
        
        // Brain
        head.Organs.Add(new Organ(OrganNames.Brain, 0.25, new CapacityContainer 
        { 
            Consciousness = 1.0 
        }, isExternal: false));

        // Eyes - excellent vision for flying
        head.Organs.Add(new Organ(OrganNames.LeftEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.7 
        }, isExternal: true));
        
        head.Organs.Add(new Organ(OrganNames.RightEye, 0.5, new CapacityContainer 
        { 
            Sight = 0.7 
        }, isExternal: true));

        // Ears - excellent hearing
        head.Organs.Add(new Organ(OrganNames.LeftEar, 1.0, new CapacityContainer 
        { 
            Hearing = 0.7 
        }, isExternal: true));
        
        head.Organs.Add(new Organ(OrganNames.RightEar, 1.0, new CapacityContainer 
        { 
            Hearing = 0.7 
        }, isExternal: true));

        parts.Add(head);

        // TORSO - 30% coverage
        var torso = new BodyRegion("Torso", 30.0);
        torso.Bone.Toughness = 8; // Hollow bones
        
        // Heart - larger for flight demands
        torso.Organs.Add(new Organ(OrganNames.Heart, 6.0, new CapacityContainer 
        { 
            BloodPumping = 1.0 
        }, isExternal: false));

        // Lungs - highly efficient
        torso.Organs.Add(new Organ(OrganNames.LeftLung, 4.0, new CapacityContainer 
        { 
            Breathing = 0.5 
        }, isExternal: false));
        
        torso.Organs.Add(new Organ(OrganNames.RightLung, 4.0, new CapacityContainer 
        { 
            Breathing = 0.5 
        }, isExternal: false));

        // Digestive organs
        torso.Organs.Add(new Organ(OrganNames.Liver, 3.5, new CapacityContainer 
        { 
            Digestion = 0.6 
        }, isExternal: false));

        torso.Organs.Add(new Organ(OrganNames.Stomach, 2.5, new CapacityContainer 
        { 
            Digestion = 0.4 
        }, isExternal: false));

        parts.Add(torso);

        // WINGS - 15% each
        var leftWing = new BodyRegion("Left Wing", 15.0);
        var rightWing = new BodyRegion("Right Wing", 15.0);
        // Wings are primarily for movement
        
        parts.AddRange([leftWing, rightWing]);

        // LEGS - 6.5% each  
        var leftLeg = new BodyRegion("Left Leg", 6.5);
        var rightLeg = new BodyRegion("Right Leg", 6.5);

        parts.AddRange([leftLeg, rightLeg]);

        return parts;
    }
}