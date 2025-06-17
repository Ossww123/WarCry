FROM openjdk:17-jdk-slim

COPY build/libs/WarcryBack-0.0.1-SNAPSHOT.jar warcry.jar

EXPOSE 8080
ENV SPRING_PROFILES_ACTIVE=prod
CMD ["java", "-jar", "/warcry.jar"]

