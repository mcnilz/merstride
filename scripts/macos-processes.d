#pragma D option quiet
#pragma D option bufsize=64m
#pragma D option switchrate=1000hz

dtrace:::BEGIN
{
    tracked[$1] = 1;
    printf("READY\n");
}

proc:::create
/tracked[pid]/
{
    tracked[args[0]->pr_pid] = 1;
    printf("%lld FORK %d %d\n", walltimestamp, args[0]->pr_pid, pid);
}

proc:::exec-success
/tracked[pid]/
{
    printf("%lld EXEC %d %d\n", walltimestamp, pid, ppid);
}

proc:::exit
/tracked[pid]/
{
    printf("%lld EXIT %d\n", walltimestamp, pid);
    tracked[pid] = 0;
}

// Observe kernel lifecycle events without a process-control attachment that
// can be lost when the stopped launcher execs the suite host.
proc:::exit
/pid == $1/
{
    exit(0);
}

profile:::tick-10ms
{
    printf("%lld SYNC\n", walltimestamp);
}

dtrace:::ERROR
{
    printf("ERROR\n");
    exit(1);
}
