# Public stranded-return validation — November 16

**PASS — targeted three-client desktop post-token recovery**, completed 13:24:47 UTC in Unity 6000.3.24f1. Three authenticated clients departed through the normal public platform countdown, lowered the bridge through actual fingertip contact, travelled from distance zero at normal 4 m/s, and completed a natural successful crossing near 856 m. The keeper supplied 40 actual coal deposits with acknowledged consumption/fuel receipts; no synthetic fuel, distance, bridge progress, finish or return token was written. [Driver](public-stranded-driver.txt), [cut client](public-stranded-cut.txt), [keeper](public-stranded-keeper.txt).

The cut client disconnected 2.025 seconds into the successful Ending. The original 32-second deadline was retained, and the production return token appeared at finish age 34.279 seconds. After the driver reached ready Compound, the cut client resumed normal authenticated retained-actor recovery and actually entered the original Returning room with that token and unchanged `finishedAt`. It requested departure **0.038729 seconds** after rejoining and reached ready Compound **2.818667 seconds** after rejoining. The keeper then restored its session and completed normal return. All three terminal logs pass and report Compound `35148`.

The keeper's local `PhotonSession` was deliberately disabled during Ending while its Photon transport remained active, retaining the old gameplay room. This controlled fault is disclosed: three normally transferring clients would otherwise leave an empty room with `EmptyRoomTtl=0`. The keeper was restored at 13:24:43.735 and reached ready Compound at 13:24:47.929. The test establishes the desktop safety-net path with a natural public finish and original token; the original worn-headset repro remains Needs human.

## First attempt and fixture correction

[Attempt1 driver](public-stranded-attempt1-driver.txt), [cut](public-stranded-attempt1-cut.txt) and [keeper](public-stranded-attempt1-keeper.txt) already established actual public departure, the complete journey, 40 physical coal receipts and natural success. Its post-token scenario was **not reached**: public room transfer assigned new actors and made the intended cut client master. The fixture had checked authority only in Compound. It aborted without disconnecting authority and restored the keeper.

The corrected fixture refreshes actor IDs after expedition arrival, requests normal SDK master reassignment if necessary, and waits for all three clients to acknowledge driver authority before contact/fuel/driving. This rerun confirmed driver actor 2, cut actor 3 and keeper actor 1; the driver was already master on arrival, so no master-handoff acceptance is inferred. No production fix was made.

## Purchase cleanup remains valid from attempt1

The [buyer prelude](compound-purchase-cleanup-buyer.txt) charged 25 coins twice, `191 → 166 → 141`. The first item disappeared after genuine buyer leave and did not replay after rejoin; the [remaining observer](compound-purchase-cleanup-main.txt) confirmed it. The second fresh token was present before boarding and absent from expedition views/current cargo manifest after attempt1's ordinary departure. That cleanup remains valid despite the later authority-fixture abort. The corrected run explicitly skipped re-claiming this already-consumed token as a fresh cleanup test.

Repeated journeys, three peer logs and corrective reruns do not represent additional independent scenario groups. No headset feel, perceived audio, active-collapse arrival or new Quest performance acceptance is claimed.
