allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

val newBuildDir: Directory =
    rootProject.layout.buildDirectory
        .dir("../../build")
        .get()
rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
}
subprojects {
    project.evaluationDependsOn(":app")
}

// Force plugins (flutter_webrtc 0.12 ships compileSdk 31, but its androidx deps need higher)
// to compile against SDK 35 so checkReleaseAarMetadata passes.
subprojects {
    afterEvaluate {
        val ext = extensions.findByName("android")
        if (ext != null) {
            try {
                ext.javaClass.getMethod("compileSdkVersion", String::class.java).invoke(ext, "android-35")
            } catch (_: Exception) { }
        }
    }
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
