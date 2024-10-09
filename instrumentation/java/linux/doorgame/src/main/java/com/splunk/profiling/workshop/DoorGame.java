package com.splunk.profiling.workshop;

import io.opentelemetry.api.trace.SpanKind;
import io.opentelemetry.instrumentation.annotations.WithSpan;

import java.util.Map;
import java.util.Random;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

public class DoorGame {

    private static final Logger LOGGER = LogManager.getLogger(DoorGame.class);

    private final DoorChecker gameOfficial = new DoorChecker();
    private final Map<String, GameInfo> games = new ConcurrentHashMap<>();

    @WithSpan(kind = SpanKind.INTERNAL)
    public String startNew() {
        LOGGER.info("Starting a new game");
        String uuid = UUID.randomUUID().toString();
        Random random = new Random();
        int winningDoor = random.nextInt(3);
        games.put(uuid, new GameInfo(uuid, winningDoor));

        return uuid;
    }

    @WithSpan(kind = SpanKind.INTERNAL)
    public int reveal(String uid) {
        LOGGER.info("Getting the door to reveal");
        GameInfo gameInfo = games.get(uid);
        return gameInfo.getDoorToReveal();
    }

    @WithSpan(kind = SpanKind.INTERNAL)
    public void pick(String uid, int picked) {
        LOGGER.info("Picking a door");
        GameInfo gameInfo = games.get(uid);
        gameInfo.pick(picked);
    }

    @WithSpan(kind = SpanKind.INTERNAL)
    public String getOutcome(String uid, int picked) {
        LOGGER.info("Determining the outcome of the game");
        GameInfo gameInfo = games.get(uid);
        return gameOfficial.isWinner(gameInfo, picked) ? "WIN" : "LOSE";
    }
}
