plugins {
    java
    id("com.github.johnrengelman.shadow") version "7.1.0"
}

repositories {
    mavenCentral()
}

tasks.withType<Jar> {
    manifest {
        attributes["Main-Class"] = "com.splunk.profiling.workshop.ServiceMain"
    }
}

dependencies {
    implementation("com.sparkjava:spark-core:2.9.4")
    implementation("org.apache.logging.log4j:log4j-api:2.24.1")
    implementation("org.apache.logging.log4j:log4j-core:2.24.1")
    implementation("io.opentelemetry:opentelemetry-api:1.42.1")
    implementation("io.opentelemetry.instrumentation:opentelemetry-instrumentation-annotations:2.6.0")
}