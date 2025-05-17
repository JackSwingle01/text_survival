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

    public static BodyPart CreateBody(BodyTypes type, double baseHP)
    {
        return type switch
        {
            BodyTypes.Human => CreateHumanBody(baseHP),
            BodyTypes.Quadruped => CreateQuadrupedBody(baseHP),
            BodyTypes.Serpentine => CreateSerpentineBody(baseHP),
            BodyTypes.Arachnid => CreateArachnidBody(baseHP),
            BodyTypes.Flying => CreateFlyingBody(baseHP),
            _ => throw new NotImplementedException("Invalid body type")
        };
    }

    // Create a snake body
    public static BodyPart CreateSerpentineBody(double hp)
    {
        // Main body
        BodyPart body = new BodyPart("Body", hp, true, false, 100);

        // Head (smaller proportion for snakes - 15%)
        BodyPart head = CreateSnakeHead(hp * 0.15);
        body.AddPart(head);

        // Internal organs
        BodyPart heart = new BodyPart("Heart", hp * 0.05, true, true, 5);
        heart.SetBaseCapacity("BloodPumping", 1.0);
        body.AddPart(heart);

        BodyPart lungs = new BodyPart("Lung", hp * 0.05, true, true, 5);
        lungs.SetBaseCapacity("Breathing", 1.0);
        body.AddPart(lungs);

        // Spine running through the body
        BodyPart spine = new BodyPart("Spine", hp * 0.15, true, true, 15);
        spine.SetBaseCapacity("Moving", 1.0);
        body.AddPart(spine);

        // Segments - create multiple body segments
        int segmentCount = 5;
        for (int i = 1; i <= segmentCount; i++)
        {
            BodyPart segment = CreateSnakeSegment(hp * 0.12, $"Segment {i}");
            body.AddPart(segment);
        }

        // Tail
        BodyPart tail = new BodyPart("Tail", hp * 0.1, false, false, 10);
        tail.SetBaseCapacity("Moving", 0.3);
        body.AddPart(tail);

        // Calculate effective coverage
        body.CalculateEffectiveCoverage();

        return body;
    }

    // Helper method for snake head
    private static BodyPart CreateSnakeHead(double hp)
    {
        BodyPart head = new BodyPart("Head", hp, true, false, 15);

        // Eyes
        BodyPart leftEye = new BodyPart("Left Eye", hp * 0.1, false, false, 5);
        leftEye.SetBaseCapacity("Sight", 0.5);
        head.AddPart(leftEye);

        BodyPart rightEye = new BodyPart("Right Eye", hp * 0.1, false, false, 5);
        rightEye.SetBaseCapacity("Sight", 0.5);
        head.AddPart(rightEye);

        // Jaw with venom glands
        BodyPart jaw = new BodyPart("Jaw", hp * 0.3, false, false, 30);
        head.AddPart(jaw);

        BodyPart venomGland = new BodyPart("Venom Gland", hp * 0.15, false, true, 15);
        jaw.AddPart(venomGland);

        BodyPart tongue = new BodyPart("Tongue", hp * 0.05, false, false, 5);
        jaw.AddPart(tongue);

        return head;
    }

    // Helper method for snake body segments
    private static BodyPart CreateSnakeSegment(double hp, string name)
    {
        BodyPart segment = new BodyPart(name, hp, false, false, 12);
        segment.SetBaseCapacity("Moving", 0.2);
        return segment;
    }

    // Create an arachnid body (spider)
    public static BodyPart CreateArachnidBody(double hp)
    {
        // Create the main body (cephalothorax + abdomen)
        BodyPart body = new BodyPart("Body", hp, true, false, 100);

        // Cephalothorax - head and thorax combined
        BodyPart cephalothorax = new BodyPart("Cephalothorax", hp * 0.4, true, false, 40);
        body.AddPart(cephalothorax);

        // Eyes - spiders have multiple eyes
        for (int i = 1; i <= 8; i++)
        {
            BodyPart eye = new BodyPart($"Eye {i}", hp * 0.01, false, false, 1);
            eye.SetBaseCapacity("Sight", 0.125); // Total sight = 8 eyes * 0.125 = 1.0
            cephalothorax.AddPart(eye);
        }

        // Fangs
        BodyPart fangs = new BodyPart("Fangs", hp * 0.05, false, false, 5);
        cephalothorax.AddPart(fangs);

        BodyPart venomGland = new BodyPart("Venom Gland", hp * 0.05, false, true, 5);
        fangs.AddPart(venomGland);

        // Heart
        BodyPart heart = new BodyPart("Heart", hp * 0.05, true, true, 5);
        heart.SetBaseCapacity("BloodPumping", 1.0);
        cephalothorax.AddPart(heart);

        // Abdomen
        BodyPart abdomen = new BodyPart("Abdomen", hp * 0.3, true, false, 30);
        body.AddPart(abdomen);

        // Silk glands in abdomen
        BodyPart silkGland = new BodyPart("Silk Gland", hp * 0.05, false, true, 5);
        abdomen.AddPart(silkGland);

        // Spinnerets
        BodyPart spinnerets = new BodyPart("Spinnerets", hp * 0.05, false, false, 5);
        abdomen.AddPart(spinnerets);

        // Eight legs
        for (int i = 1; i <= 8; i++)
        {
            string legPosition;
            if (i <= 4)
                legPosition = $"Front {(i <= 2 ? "Left" : "Right")} Leg {(i % 2 == 0 ? 2 : 1)}";
            else
                legPosition = $"Rear {(i <= 6 ? "Left" : "Right")} Leg {(i % 2 == 0 ? 2 : 1)}";

            BodyPart leg = CreateArachnidLeg(hp * 0.025, legPosition);
            body.AddPart(leg);
        }

        // Calculate effective coverage
        body.CalculateEffectiveCoverage();

        return body;
    }

    // Helper method for spider leg
    private static BodyPart CreateArachnidLeg(double hp, string name)
    {
        BodyPart leg = new BodyPart(name, hp, false, false, 2.5);
        leg.SetBaseCapacity("Moving", 0.125); // Each leg contributes 1/8 of total movement

        // Leg segments
        BodyPart femur = new BodyPart("Femur", hp * 0.3, false, false, 30);
        leg.AddPart(femur);

        BodyPart patella = new BodyPart("Patella", hp * 0.2, false, false, 20);
        leg.AddPart(patella);

        BodyPart tibia = new BodyPart("Tibia", hp * 0.3, false, false, 30);
        leg.AddPart(tibia);

        BodyPart tarsus = new BodyPart("Tarsus", hp * 0.2, false, false, 20);
        leg.AddPart(tarsus);

        return leg;
    }

    // Create a flying body (for bats)
    public static BodyPart CreateFlyingBody(double hp)
    {
        // Torso
        BodyPart torso = new BodyPart("Torso", hp, true, false, 100);

        // Head
        BodyPart head = CreateHead(hp * 0.15);
        torso.AddPart(head);

        // Internal organs
        BodyPart heart = CreateHeart(hp * 0.05);
        torso.AddPart(heart);

        BodyPart leftLung = CreateLungs(hp * 0.05, "Left Lung");
        torso.AddPart(leftLung);

        BodyPart rightLung = CreateLungs(hp * 0.05, "Right Lung");
        torso.AddPart(rightLung);

        // Wings (modified arms/forelimbs)
        BodyPart leftWing = CreateWing(hp * 0.2, "Left Wing");
        torso.AddPart(leftWing);

        BodyPart rightWing = CreateWing(hp * 0.2, "Right Wing");
        torso.AddPart(rightWing);

        // Legs
        BodyPart leftLeg = CreateLeg(hp * 0.1, "Left Leg");
        torso.AddPart(leftLeg);

        BodyPart rightLeg = CreateLeg(hp * 0.1, "Right Leg");
        torso.AddPart(rightLeg);

        // Calculate effective coverage
        torso.CalculateEffectiveCoverage();

        return torso;
    }

    // Helper method for wings
    private static BodyPart CreateWing(double hp, string name)
    {
        BodyPart wing = new BodyPart(name, hp, false, false, 20);
        wing.SetBaseCapacity("Moving", 0.5); // Wings provide flight capability

        // Wing structure includes modified arm bones and wing membrane
        BodyPart humerus = new BodyPart("Humerus", hp * 0.2, false, false, 20);
        wing.AddPart(humerus);

        BodyPart radius = new BodyPart("Radius", hp * 0.15, false, false, 15);
        wing.AddPart(radius);

        BodyPart digits = new BodyPart("Digits", hp * 0.15, false, false, 15);
        wing.AddPart(digits);

        BodyPart membrane = new BodyPart("Membrane", hp * 0.5, false, false, 50);
        wing.AddPart(membrane);

        return wing;
    }
    public static BodyPart CreateHumanBody(double hp)
    {
        // Torso (main part)
        BodyPart torso = new BodyPart("Torso", hp, true, false, 100);

        // Neck (7.5% of torso)
        BodyPart neck = CreateNeck(hp * 0.075);
        torso.AddPart(neck);

        // Spine (2.5% of torso)
        BodyPart spine = CreateSpine(hp * .025);
        torso.AddPart(spine);

        // Ribcage (3.6% of torso)
        BodyPart ribcage = CreateRibcage(hp * 0.036);
        torso.AddPart(ribcage);

        // Sternum (1.5% of torso)
        BodyPart sternum = CreateSternum(hp * 0.015);
        torso.AddPart(sternum);

        // Internal organs
        // Stomach (2.5% of torso)
        BodyPart stomach = CreateStomach(hp * 0.025);
        torso.AddPart(stomach);

        // Heart (2.0% of torso)
        BodyPart heart = CreateHeart(hp * 0.02);
        torso.AddPart(heart);

        // Lungs (2.5% each, total 5.0% of torso)
        BodyPart leftLung = CreateLungs(hp * 0.025, "Left Lung");
        torso.AddPart(leftLung);

        BodyPart rightLung = CreateLungs(hp * 0.025, "Right Lung");
        torso.AddPart(rightLung);

        // Kidneys (1.7% each, total 3.4% of torso)
        BodyPart leftKidney = CreateKidney(hp * 0.017, "Left Kidney");
        torso.AddPart(leftKidney);

        BodyPart rightKidney = CreateKidney(hp * 0.017, "Right Kidney");
        torso.AddPart(rightKidney);

        // Liver (2.5% of torso)
        BodyPart liver = CreateLiver(hp * 0.025);
        torso.AddPart(liver);

        // Pelvis (2.5% of torso)
        BodyPart pelvis = CreatePelvis(hp * 0.025);
        torso.AddPart(pelvis);

        // Shoulders (12% of torso, 6% each)
        BodyPart leftShoulder = CreateShoulder(hp * 0.06, "Left Shoulder");
        torso.AddPart(leftShoulder);

        BodyPart rightShoulder = CreateShoulder(hp * 0.06, "Right Shoulder");
        torso.AddPart(rightShoulder);

        // Legs (14% of torso, 7% each)
        BodyPart leftLeg = CreateLeg(hp * 0.07, "Left Leg");
        torso.AddPart(leftLeg);

        BodyPart rightLeg = CreateLeg(hp * 0.07, "Right Leg");
        torso.AddPart(rightLeg);

        // Calculate effective coverage
        torso.CalculateEffectiveCoverage();

        return torso;
    }

    public static BodyPart CreateQuadrupedBody(double hp)
    {

        // Torso
        BodyPart torso = new BodyPart("Torso", hp, true, false, 100);

        // Head (10% of torso)
        BodyPart head = CreateHead(hp * 0.625);
        torso.AddPart(head);

        // Internal organs with appropriate coverage
        BodyPart heart = CreateHeart(hp * 0.375);
        torso.AddPart(heart);

        BodyPart leftLung = CreateLungs(hp * 0.375, "Left Lung");
        torso.AddPart(leftLung);

        BodyPart rightLung = CreateLungs(hp * 0.375, "Right Lung");
        torso.AddPart(rightLung);

        // Legs (higher coverage for quadrupeds)
        BodyPart frontLeftLeg = CreateLeg(hp * 0.75, "Front Left Leg");
        torso.AddPart(frontLeftLeg);

        BodyPart frontRightLeg = CreateLeg(hp * 0.75, "Front Right Leg");
        torso.AddPart(frontRightLeg);

        BodyPart rearLeftLeg = CreateLeg(hp * 0.75, "Rear Left Leg");
        torso.AddPart(rearLeftLeg);

        BodyPart rearRightLeg = CreateLeg(hp * 0.75, "Rear Right Leg");
        torso.AddPart(rearRightLeg);

        // Calculate effective coverage
        torso.CalculateEffectiveCoverage();

        return torso;
    }

    public static BodyPart CreateGenericBody(string name, double hp)
    {
        return new BodyPart(name, hp, true, false, 100);
    }

    public static BodyPart CreateNeck(double hp)
    {
        BodyPart neck = new BodyPart("Neck", hp, true, false, 7.5);
        neck.SetBaseCapacity("Eating", 0.5);
        neck.SetBaseCapacity("Talking", 0.5);
        neck.SetBaseCapacity("Breathing", 0.5);

        // Head (80% of neck)
        BodyPart head = CreateHead(hp);
        neck.AddPart(head);

        return neck;
    }

    public static BodyPart CreateHead(double hp)
    {
        BodyPart head = new BodyPart("Head", hp, true, false, 80.0);

        // Skull (18% of head)
        BodyPart skull = new BodyPart("Skull", hp * 0.625, false, true, 18.0);
        head.AddPart(skull);

        // Brain (80% of skull)
        BodyPart brain = CreateBrain(hp * 0.25);
        skull.AddPart(brain);

        // Eyes (7% each, total 14% of head)
        BodyPart rightEye = CreateEye(hp * 0.25, "Right Eye");
        head.AddPart(rightEye);

        BodyPart leftEye = CreateEye(hp * 0.25, "Left Eye");
        head.AddPart(leftEye);

        // Ears (7% each, total 14% of head)
        BodyPart leftEar = CreateEar(hp * 0.3, "Left Ear");
        head.AddPart(leftEar);

        BodyPart rightEar = CreateEar(hp * 0.3, "Right Ear");
        head.AddPart(rightEar);

        // Nose (10% of head)
        BodyPart nose = new BodyPart("Nose", hp * 0.25, false, false, 10.0);
        head.AddPart(nose);

        // Jaw (15% of head)
        BodyPart jaw = CreateJaw(hp * 0.5);
        head.AddPart(jaw);

        return head;
    }

    public static BodyPart CreateShoulder(double hp, string name = "Shoulder")
    {
        BodyPart shoulder = new BodyPart(name, hp, false, false, 6.0);
        shoulder.SetBaseCapacity("Manipulation", 0.5);

        // Clavicle (9% of shoulder)
        BodyPart clavicle = CreateClavicle(hp * 0.625);
        shoulder.AddPart(clavicle);

        // Arm (77% of shoulder)
        BodyPart arm = CreateArm(hp * 0.75, name.Replace("Shoulder", "Arm"));
        shoulder.AddPart(arm);

        return shoulder;
    }

    public static BodyPart CreateArm(double hp, string name = "Arm")
    {
        BodyPart arm = new BodyPart(name, hp, false, false, 77.0);
        arm.SetBaseCapacity("Manipulation", 0.5);

        // Humerus (10% of arm)
        BodyPart humerus = CreateHumerus(hp * 0.625);
        arm.AddPart(humerus);

        // Radius (10% of arm)
        BodyPart radius = CreateRadius(hp * 0.5);
        arm.AddPart(radius);

        // Hand (14% of arm)
        BodyPart hand = CreateHand(hp * 0.5, name.Replace("Arm", "Hand"));
        arm.AddPart(hand);

        return arm;
    }

    public static BodyPart CreateHand(double hp, string name = "Hand")
    {
        BodyPart hand = new BodyPart(name, hp, false, false, 14.0);
        hand.SetBaseCapacity("Manipulation", 0.5);

        // Fingers (each with appropriate coverage of hand)
        BodyPart thumb = CreateFinger(hp * 0.2, name.Replace("Hand", "Thumb"));
        hand.AddPart(thumb);

        BodyPart indexFinger = CreateFinger(hp * 0.2, name.Replace("Hand", "Index Finger"));
        hand.AddPart(indexFinger);

        BodyPart middleFinger = CreateFinger(hp * 0.2, name.Replace("Hand", "Middle Finger"));
        hand.AddPart(middleFinger);

        BodyPart ringFinger = CreateFinger(hp * 0.2, name.Replace("Hand", "Ring Finger"));
        hand.AddPart(ringFinger);

        BodyPart pinky = CreateFinger(hp * 0.2, name.Replace("Hand", "Pinky"));
        hand.AddPart(pinky);

        return hand;
    }

    public static BodyPart CreateFinger(double hp, string name = "Finger")
    {
        BodyPart finger = new BodyPart(name, hp, false, false, 7.0); // Average coverage
        finger.SetBaseCapacity("Manipulation", 0.08);
        return finger;
    }

    public static BodyPart CreateLeg(double hp, string name = "Leg")
    {
        BodyPart leg = new BodyPart(name, hp, false, false, 7.0);
        leg.SetBaseCapacity("Moving", 0.5);

        // Femur (10% of leg)
        BodyPart femur = CreateFemur(hp * 0.625);
        leg.AddPart(femur);

        // Tibia (10% of leg)
        BodyPart tibia = CreateTibia(hp * 0.625);
        leg.AddPart(tibia);

        // Foot (10% of leg)
        BodyPart foot = CreateFoot(hp * 0.625, name.Replace("Leg", "Foot"));
        leg.AddPart(foot);

        return leg;
    }

    public static BodyPart CreateFoot(double hp, string name = "Foot")
    {
        BodyPart foot = new BodyPart(name, hp, false, false, 10.0);
        foot.SetBaseCapacity("Moving", 0.5);

        // Toes (each with appropriate coverage of foot)
        BodyPart bigToe = CreateToe(hp * 0.2, name.Replace("Foot", "Big Toe"));
        foot.AddPart(bigToe);

        BodyPart secondToe = CreateToe(hp * 0.2, name.Replace("Foot", "Second Toe"));
        foot.AddPart(secondToe);

        BodyPart middleToe = CreateToe(hp * 0.2, name.Replace("Foot", "Middle Toe"));
        foot.AddPart(middleToe);

        BodyPart fourthToe = CreateToe(hp * 0.2, name.Replace("Foot", "Fourth Toe"));
        foot.AddPart(fourthToe);

        BodyPart littleToe = CreateToe(hp * 0.2, name.Replace("Foot", "Little Toe"));
        foot.AddPart(littleToe);

        return foot;
    }

    public static BodyPart CreateToe(double hp, string name = "Toe")
    {
        BodyPart toe = new BodyPart(name, hp, false, false, 7.0); // Average coverage
        toe.SetBaseCapacity("Moving", 0.04);
        return toe;
    }

    public static BodyPart CreateBrain(double hp)
    {
        BodyPart brain = new BodyPart("Brain", hp, true, true, 80.0);
        brain.SetBaseCapacity("Consciousness", 1.0);
        return brain;
    }

    public static BodyPart CreateEye(double hp, string name = "Eye")
    {
        BodyPart eye = new BodyPart(name, hp, false, false, 7.0);
        eye.SetBaseCapacity("Sight", 0.5);
        return eye;
    }

    public static BodyPart CreateMouth(double hp)
    {
        BodyPart mouth = new BodyPart("Mouth", hp, false, false, 15.0);
        mouth.SetBaseCapacity("Eating", 0.5);
        mouth.SetBaseCapacity("Talking", 0.5);
        return mouth;
    }

    public static BodyPart CreateJaw(double hp)
    {
        BodyPart jaw = new BodyPart("Jaw", hp, false, false, 15.0);
        jaw.SetBaseCapacity("Eating", 0.5);
        jaw.SetBaseCapacity("Talking", 0.5);

        // Tongue (0.1% of jaw as per RimWorld)
        BodyPart tongue = CreateTongue(hp * 0.25);
        jaw.AddPart(tongue);

        return jaw;
    }

    public static BodyPart CreateTongue(double hp)
    {
        BodyPart tongue = new BodyPart("Tongue", hp, false, true, 0.1);
        tongue.SetBaseCapacity("Talking", 0.5);
        return tongue;
    }

    public static BodyPart CreateLungs(double hp, string name = "Lungs")
    {
        BodyPart lungs = new BodyPart(name, hp, true, true, 2.5);
        lungs.SetBaseCapacity("Breathing", 0.5);
        return lungs;
    }

    public static BodyPart CreateEar(double hp, string name = "Ear")
    {
        BodyPart ear = new BodyPart(name, hp, false, false, 7.0);
        ear.SetBaseCapacity("Hearing", 0.5);
        return ear;
    }

    public static BodyPart CreateHeart(double hp)
    {
        BodyPart heart = new BodyPart("Heart", hp, true, true, 2.0);
        heart.SetBaseCapacity("BloodPumping", 1.0);
        return heart;
    }

    public static BodyPart CreateStomach(double hp)
    {
        BodyPart stomach = new BodyPart("Stomach", hp, true, true, 2.5);
        stomach.SetBaseCapacity("Digestion", 0.5);
        return stomach;
    }

    public static BodyPart CreateLiver(double hp)
    {
        BodyPart liver = new BodyPart("Liver", hp, true, true, 2.5);
        liver.SetBaseCapacity("Digestion", 0.5);
        return liver;
    }

    public static BodyPart CreateKidney(double hp, string name = "Kidney")
    {
        BodyPart kidney = new BodyPart(name, hp, true, true, 1.7);
        kidney.SetBaseCapacity("BloodFiltration", 0.5);
        return kidney;
    }

    public static BodyPart CreateSpine(double hp)
    {
        BodyPart spine = new BodyPart("Spine", hp, false, true, 2.5);
        spine.SetBaseCapacity("Moving", 1.0);
        return spine;
    }

    public static BodyPart CreateRibcage(double hp)
    {
        BodyPart ribcage = new BodyPart("Ribcage", hp, false, true, 3.6);
        ribcage.SetBaseCapacity("Breathing", 0.5);
        return ribcage;
    }

    public static BodyPart CreateSternum(double hp)
    {
        BodyPart sternum = new BodyPart("Sternum", hp, false, true, 1.5);
        sternum.SetBaseCapacity("Breathing", 0.5);
        return sternum;
    }

    public static BodyPart CreatePelvis(double hp)
    {
        BodyPart pelvis = new BodyPart("Pelvis", hp, false, true, 2.5);
        pelvis.SetBaseCapacity("Moving", 1.0);
        return pelvis;
    }

    public static BodyPart CreateClavicle(double hp)
    {
        BodyPart clavicle = new BodyPart("Clavicle", hp, false, true, 9.0);
        clavicle.SetBaseCapacity("Manipulation", 0.5);
        return clavicle;
    }

    public static BodyPart CreateHumerus(double hp)
    {
        BodyPart humerus = new BodyPart("Humerus", hp, false, true, 10.0);
        humerus.SetBaseCapacity("Manipulation", 0.5);
        return humerus;
    }

    public static BodyPart CreateRadius(double hp)
    {
        BodyPart radius = new BodyPart("Radius", hp, false, true, 10.0);
        radius.SetBaseCapacity("Manipulation", 0.5);
        return radius;
    }

    public static BodyPart CreateFemur(double hp)
    {
        BodyPart femur = new BodyPart("Femur", hp, false, true, 10.0);
        femur.SetBaseCapacity("Moving", 0.5);
        return femur;
    }

    public static BodyPart CreateTibia(double hp)
    {
        BodyPart tibia = new BodyPart("Tibia", hp, false, true, 10.0);
        tibia.SetBaseCapacity("Moving", 0.5);
        return tibia;
    }
}
