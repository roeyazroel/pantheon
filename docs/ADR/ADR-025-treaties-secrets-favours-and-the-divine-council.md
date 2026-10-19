# ADR-025: Treaties, secret agreements, favours and the Divine Council

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-012](ADR-012-diplomacy-relations-status-and-memory.md) (relations, status and memory),
  [ADR-015](ADR-015-four-gods-coalitions-and-pantheon-victory.md) (coalitions and pantheon victory)
  and [ADR-024](ADR-024-doctrines-secularism-dependency-resentment-and-traits.md) (conduct and traits)

## Context

Diplomacy offered alliance, peace and war. The specification asks for more:

- **§38:** treaties (non-aggression, defensive pact, joint war, shared influence, religious access,
  Creature non-aggression, disaster assistance, mutual defence, territorial agreement, tribute,
  migration agreement) with durations and conditions.
- **§39:** secret agreements: a god publicly allied with A may secretly promise C to join it
  against A.
- **§40:** any treaty may be broken. Betrayal stays viable, but it has consequences for trust,
  global reputation, future treaties, mortal opinion, allies and credibility.
- **§41:** gods remember major help. A favour is owed, and may later be called in for war
  support, territory, miracle assistance, Divine Power or political backing.
- **§42:** periodic major events summon a Divine Council. It votes to condemn a god, protect
  sacred land, prohibit a practice, coordinate disaster response, declare pantheon-wide war or
  establish neutral territory. Votes may be public, secret, traded or negotiated.
- **§54:** near a joint victory, an ally may betray its partner or absorb its believers.

## Decision

### Treaties

- **State:** `DiplomacyState.Treaties` holds each `Treaty`:
  - its kind and its two parties (the proposer first);
  - a target, for a joint war (required) or a defensive pact (optional);
  - a payer and an amount, for a tribute;
  - whether it is secret, when it was signed, and when it ends (or never).
- **Proposing and answering:** a god proposes a treaty with `ProposeTreaty` and its `TreatyTerms`,
  as a proposal like an alliance. Accepting signs it (`TreatySigned`) and notes faithfulness on
  both sides.
- **Checks (the authority):**
  - Terms must be valid: a term up to `MaxTreatySeconds`; targets other than the parties; a
    tribute's payer is a party and its amount at most `MaxTribute`.
  - At war only a non-aggression pact or a tribute may be proposed, and signing either makes
    peace.
  - The same treaty may not be signed twice.
- **Effects:** each lives in `TreatyModel`, asked by the system it changes.

  | Treaty | Effect |
  | --- | --- |
  | Non-aggression | War between the parties is refused (`TreatyForbids`) until the pact is broken. |
  | Defensive pact | When the target (or anyone) declares war on the second party, the first goes to war with the aggressor. A secret pact comes to light; honouring it against an ally breaks that alliance. |
  | Mutual defence | Either party joins the other's wars against any aggressor. |
  | Joint war | Both parties go to war with the target at signing, and neither may make a separate peace. |
  | Shared influence | Each casts in the other's influence as an ally would (no contested cost). |
  | Religious access | Each party's temples send missionaries to every village the other leads, and isolationism there does not shut it out. |
  | Creature non-aggression | Their Creatures never fight: a fight at a meeting becomes ignoring. |
  | Disaster assistance | The AI counts the partner's people struck by disaster half as its own when it chooses help. |
  | Territorial agreement | Neither may cast a miracle reaching a village the other leads (`CastRejection.TreatyForbids`). |
  | Tribute | The payer pays the amount each minute; a payer that cannot pay breaks the treaty. |
  | Migration agreement | Each welcomes the other's migrants whatever its refugee policy. |

- **Ending:** a treaty runs its term (`TreatyEnded`, unbroken). A war declared between the parties
  breaks their treaties, as the declarer.
- **Breaking (§40):** `BreakTreaty` ends a treaty and is a betrayal:
  - the partner remembers `BrokeTreaty`, losing `TreatyBetrayalShare` of an alliance's betrayal in
    trust and gaining suspicion; its allies are told;
  - every other god that knew of the treaty trusts the breaker less;
  - the world notes deceit, so a breaker earns the Deceptive trait, and AI gods weigh an honest or
    deceptive proposer's word when they answer.

### Secret agreements (§39)

- **Who knows:** a secret treaty is known only to its parties. Other gods' readouts and the AI
  never see it.
- **In multiplayer:** the host strips it from the world, the diplomacy section and the events it
  sends other players (`SecretFilter`).
- **Coming to light:** it is revealed when it is invoked, or at random, `SecretLeakPerSecond` a
  second, from a stream derived from the seed and the tick (`TreatyRevealed`). The god it was aimed
  at remembers both parties: `Betrayed` if allied with them, otherwise `PlottedAgainst`.

### Favours, requests and promises (§41)

- **Favours:** a miracle that answers great need (at least 0.75) among another god's followers is
  a rescue. That god owes the caster a favour (`Favour`, up to `MaxFavours`), and `FavourOwed` is
  raised.
- **Requests:** any god may ask another with `Request` and a `RequestAsk`.

  | Ask | When granted |
  | --- | --- |
  | Divine Power | The power is transferred. |
  | War support | The asked god declares war on a named god (with the usual pacts). |
  | Territory | A territorial agreement is signed for a decree's length. |
  | Assistance | A promise to help the asker's people in need within `AssistSeconds`. |
  | Backing | A promise to vote as the asker does at the next council. |

- **Granting and refusing:** granting repays one favour and earns trust (`KeptWord`). Refusing a
  god owed a favour forfeits the favour and costs `BrokenPromiseTrustLoss` (`BrokePromise`).
  `RequestAnswered` is raised either way.
- **Promises:** an `Obligation` is kept when the debtor helps the creditor's people, or votes as
  it did. It is broken when the deadline passes or the vote differs (`ObligationEnded`).

### The Divine Council (§42)

- **State:** `DiplomacyState.Council` holds the session open now, the tabled motions, the decrees
  in force, the next regular session and the last close.
- **What summons it** (`CouncilChamber`, at the diplomacy step's rate):
  - Event triggers, no sooner than `CouncilCooldownSeconds` after the last session:
    - a betrayal of an ally since the last session: condemn the betrayer, tabled by the victim;
    - a god newly dominating: a pantheon-wide war on the dominating god with the most followers;
    - a quake, fire or flood that entered the history: coordinate the response.
  - Otherwise, a regular session every `CouncilIntervalSeconds` takes up the oldest tabled motion.
- **Voting:**
  - The vote stays open `VoteSeconds`. A motion passes with more votes for than against and at
    least two for (one in a world of two gods).
  - In a public ballot, the god a motion names remembers who voted for it, and the proposer who
    voted against it (`VotedAgainst`). A secret ballot leaves no memory and shows only a tally.
- **Traded and negotiated votes:** a backing promise binds a vote, so votes can be traded for a
  favour or requested.
- **Motions and decrees:** a decree holds for `DecreeSeconds`. A motion that repeats a decree in
  force is not valid.

  | Motion | Decree |
  | --- | --- |
  | Condemn a god | Every other god's trust in it falls by `CondemnTrustLoss`, and its followers' doubt rises by `CondemnDoubt` (mortal opinion). AI gods weigh war on it more. |
  | Protect sacred land | No harmful miracle may fall on the holy site. |
  | Prohibit a practice | Smiting (lightning, judgement), cataclysms (fire, quakes, floods) or terror. |
  | Coordinate disaster response | Help reaching a settlement struck within `StruckSeconds` costs `CoordinatedCostFactor`. |
  | Pantheon-wide war | Every god that voted for it goes to war with the target. |
  | Neutral territory | No god converts in the settlement, and no harm may fall there. |

- **Defiance:** decrees bind by reputation, not force. A cast that defies one is allowed, but every
  other god remembers `DefiedCouncil` and trusts the caster less (`DefianceTrustLoss`), and the
  world notes deceit (`CouncilDefied`).
- **Commands:** `CouncilAuthority` checks tabling (a valid motion, one waiting per god) and voting
  (a vote may change until the count).

### Absorbing an ally (§54)

`Absorb` breaks an alliance as a betrayal. In every settlement where the absorbing god is followed
more than its ally, `AbsorbShare` of the ally's followers turn to it (`AllyAbsorbed`).

### The AI

- **`TreatyAdvisor`:**
  - It proposes the treaty it wants most:
    - non-aggression when weary, afraid or losing;
    - mutual defence and shared influence among friends;
    - a defensive pact against the leader, secret when it is allied with the leader or
      treacherous;
    - a joint war against a common enemy;
    - a tribute from a much weaker enemy;
    - and the others by cooperation and goodwill.
  - It proposes to another AI god only what that god might accept.
  - It breaks a pact that keeps it from a war it wants, or a tribute it no longer fears to stop.
  - It calls in a favour for war on its enemy, power when poor, or backing in session.
- **Answers:** trust, goodwill and the proposer's word decide cooperative treaties. Peace pacts are
  weighed like peace: by weariness, fear and the balance. A god does not agree to a tribute it
  cannot afford.
- **`CouncilAdvisor`:**
  - It votes as it promised, for its own motion, and against one aimed at it; otherwise by the
    motion's worth to it.
  - It tables a condemnation of a god it bears a deep grudge, a pantheon war on a dominant rival, a
    coordinated response when its people were struck, protection of a site it keeps, or a
    prohibition of cataclysms a rival works.

### Saves, multiplayer and presentation

- **Saves:** version 17. Migrating from 16 gives the world no treaties, favours or promises, and a
  council that has not met. `PoliticsIntegrity` checks ids, parties, terms, motions and votes.
- **Multiplayer:** protocol 5. `DiplomacyIntent` carries terms, a treaty and an ask, and
  `CouncilIntent` tables a motion or votes. Secrets are filtered for each player.
- **The diplomacy panel (P):** for each god:
  - the player's treaties with it, each with Break;
  - its other known treaties;
  - favours and promises;
  - its proposal in words;
  - under "Treaties and requests", every treaty the authority would accept now (with a term and
    secrecy chosen in the panel) and every request.
- **The council panel (U):** the session, votes or tally, the ballot, decrees, tabled motions,
  and the motions the player may table.
- **Alerts:** treaties signed, broken, revealed and invoked; favours; requests answered; promises
  broken; councils summoned and closed; defiance; absorption.
- **History:** public treaties of note, revelations, decrees and absorptions.
- **Debug:** F1 lists every treaty (secrets included), favour, promise and the council. J summons
  the council. `--treaty`, `--council-panel` and `--summon-council` drive politics from the
  command line.

## Consequences

- **AI politics:** in ten passive-player games the AI gods sign defensive pacts, joint wars,
  disaster assistance, territorial agreements, mutual defence, non-aggression and tributes, and
  the council passes condemnations, coordinated responses and pantheon wars.
- **Balance:** measured over 40 seeds against part 4's build (see the plan). As many games are
  won, and the median time holds.
- **Tuning found:**
  - Peace pacts must not be blocked by grudges.
  - Proposals to other AI gods must be plausible, or they are spammed and declined.
  - Tribute must be affordable, or it defaults and every default summons a council.
- **Not modelled:**
  - Treaty conditions beyond a term, a target, a payer and an amount.
  - Votes traded for anything but backing.
  - A miracle assistance carried out by the host on the debtor's behalf: it is a promise the
    debtor must keep.
- **Next:** part 6 builds war goals, intelligence and deception, proxy wars and mortal holy wars
  (§43–46) on these treaties and the council.
