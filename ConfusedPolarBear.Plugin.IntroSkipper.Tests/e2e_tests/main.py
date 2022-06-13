import argparse, json, os, time
import requests

# Server address
addr = ""

# Authentication token
token = ""

# GUID of the analyze episodes scheduled task
taskId = "8863329048cc357f7dfebf080f2fe204"

# Parse CLI arguments
def parse_args():
    parser = argparse.ArgumentParser()

    parser.add_argument(
        "-s",
        help="Server address (if different than http://127.0.0.1:8096)",
        type=str,
        dest="address",
        default="http://127.0.0.1:8096",
        metavar="ADDRESS",
    )

    parser.add_argument(
        "-freq",
        help="Interval to poll task completion state at (default is 10 seconds)",
        type=int,
        dest="frequency",
        default=10,
    )

    parser.add_argument(
        "-f",
        help="Expected intro timestamps (as previously retrieved from /Intros/All)",
        type=str,
        dest="expected",
        default="expected/dev.json",
        metavar="FILENAME",
    )

    return parser.parse_args()


# Send an HTTP request and return the response
def send(url, method="GET", log=True):
    global addr, token

    # Construct URL
    r = None
    url = addr + url

    # Log request
    if log:
        print(f"{method} {url} ", end="")

    # Send auth token
    headers = {"Authorization": f"MediaBrowser Token={token}"}

    # Send the request
    if method == "GET":
        r = requests.get(url, headers=headers)
    elif method == "POST":
        r = requests.post(url, headers=headers)
    else:
        raise ValueError(f"Unknown method {method}")

    # Log status code
    if log:
        print(f"{r.status_code}\n")

    # Check status code
    r.raise_for_status()

    return r


def close_enough(expected, actual):
    # TODO: make customizable
    return abs(expected - actual) <= 2


# Validate that all episodes in actual have a similar entry in expected.
def validate(expected, actual):
    good = 0
    bad = 0
    total = len(expected)

    for i in expected:
        if i not in actual:
            print(f"[!] Cound not find episode {i}")
            bad += 1
            continue

        ex = expected[i]
        ac = actual[i]

        start = close_enough(ex["IntroStart"], ac["IntroStart"])
        end = close_enough(ex["IntroEnd"], ac["IntroEnd"])

        # If both the start and end times are close enough, keep going
        if start and end:
            good += 1
            continue

        # Oops
        bad += 1

        print(f"[!] Episode {i} is not correct")
        print(
            f"expected {ex['IntroStart']} => {ex['IntroEnd']} but found {ac['IntroStart']} => {ac['IntroEnd']}"
        )

    print()

    print("Statistics:")
    print(f"Correct:   {good} ({int((good * 100) / total)}%)")
    print(f"Incorrect: {bad}")
    print(f"Total:     {total}")


def main():
    global addr, token

    # Validate arguments
    args = parse_args()
    addr = args.address

    # Validate token
    token = os.environ.get("JELLYFIN_TOKEN")
    if token is None:
        print(
            "Administrator access token is required, set environment variable JELLYFIN_TOKEN and try again"
        )
        exit(1)

    # Validate expected timestamps
    expected = []
    with open(args.expected, "r") as f:
        expected = json.load(f)

    print(f"[+] Found {len(expected)} expected timestamps\n")

    # Erase old intro timestamps
    print("[+] Erasing previously discovered introduction timestamps")
    send("/Intros/EraseTimestamps", "POST")

    # Run analyze episodes task
    print("[+] Starting episode analysis task")
    send(f"/ScheduledTasks/Running/{taskId}", "POST")

    # Poll for completion
    print("[+] Waiting for analysis task to complete")

    while True:
        time.sleep(args.frequency)
        task = send(f"/ScheduledTasks/{taskId}", "GET", False).json()
        state = task["State"]

        # Calculate percentage analyzed
        percent = 0

        if state == "Idle":
            percent = 100

        elif state == "Running":
            percent = 0
            if "CurrentProgressPercentage" in task:
                percent = task["CurrentProgressPercentage"]

        # Print percentage analyzed
        print(f"\r[+] Episodes analyzed: {percent}%", end="")
        if percent == 100:
            print("\n")
            break

    # Download actual intro timestamps
    print("[+] Getting actual timestamps")
    intros = send("/Intros/All")

    actual = intros.json()

    # Store actual episodes to the filesystem
    with open("/tmp/actual.json", "w") as f:
        f.write(intros.text)

    # Verify timestamps
    print(f"[+] Found {len(actual)} actual timestamps\n")

    validate(expected, actual)

    # TODO: Pick 5 random intros and verify deeply equal to v1
    # this should be done by getting the versioned endpoint and non-versioned


main()
